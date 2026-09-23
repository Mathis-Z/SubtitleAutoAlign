using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Runs Jellyfin 12.1 with the plugin (including the bundled ffsubsync) and the
/// "Us Now" clip in /media, sets it up through its HTTP API and adds a
/// deliberately shifted subtitle, the way a subtitle download would.
/// The API is called with curl inside the container, so no host port is needed.
/// </summary>
public sealed class JellyfinEndToEndFixture : IAsyncLifetime
{
    public const string MovieDirectory = "/media/UsNow";

    private readonly string _pluginDirectory = Directory.CreateTempSubdirectory("subtitle-auto-align-plugin-").FullName;

    public IContainer Container { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await PluginTestPackage.PublishAsync(_pluginDirectory);
        var ffsubsync = await PluginTestPackage.GetFfSubSyncBinaryAsync();
        var video = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "us-now-5min.mp4"));

        const UnixFileModes Executable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute
            | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;

        Container = new ContainerBuilder(PluginTestPackage.JellyfinImage)
            .WithResourceMapping(new DirectoryInfo(_pluginDirectory), $"{PluginTestPackage.PluginPath}/")
            .WithResourceMapping(ffsubsync, $"{PluginTestPackage.PluginPath}/bundled/linux-x64/ffsubsync", fileMode: Executable)
            .WithResourceMapping(video, $"{MovieDirectory}/us-now-5min.mp4")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Startup complete"))
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }

        Directory.Delete(_pluginDirectory, recursive: true);
    }
}

[Trait("Category", "JellyfinContainer")]
public class JellyfinEndToEndTests : IClassFixture<JellyfinEndToEndFixture>
{
    private const string ClientAuthorization = "MediaBrowser Client=\"integration-tests\", Device=\"integration-tests\", DeviceId=\"integration-tests\", Version=\"1.0.0\"";
    private static readonly TimeSpan Shift = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(1);

    // Warnings SubtitleAlignmentService logs when an alignment attempt fails.
    private static readonly string[] FailureMarkers = ["ffsubsync exited with code", "Failed to run ffsubsync", "ffsubsync timed out"];

    private readonly IContainer _container;

    public JellyfinEndToEndTests(JellyfinEndToEndFixture fixture)
    {
        _container = fixture.Container;
    }

    [Fact]
    public async Task NewSubtitle_IsAutoAlignedNextToTheMovie()
    {
        var token = await CompleteSetupAndLogInAsync();

        await ApiAsync("POST", "/Library/VirtualFolders?name=Movies&collectionType=movies&refreshLibrary=true", token,
            """{"LibraryOptions":{"PathInfos":[{"Path":"/media"}]}}""");
        var movieId = await WaitForAsync("the movie to be added to the library", TimeSpan.FromMinutes(2), async () =>
        {
            using var items = JsonDocument.Parse(await ApiAsync("GET", "/Items?Recursive=true&IncludeItemTypes=Movie", token));
            return items.RootElement.GetProperty("Items").EnumerateArray().Select(i => i.GetProperty("Id").GetString()).FirstOrDefault();
        });

        // A downloaded subtitle lands next to the movie and the item gets refreshed.
        var original = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "us-now-5min.en.srt"));
        await _container.CopyAsync(Encoding.UTF8.GetBytes(SrtTimings.Shift(original, Shift)), $"{JellyfinEndToEndFixture.MovieDirectory}/us-now-5min.en.srt");
        await ApiAsync("POST", $"/Items/{movieId}/Refresh?metadataRefreshMode=Default&imageRefreshMode=Default", token);

        var alignedPath = $"{JellyfinEndToEndFixture.MovieDirectory}/us-now-5min.en.autoaligned.srt";
        await WaitForAsync("the plugin to write " + alignedPath, TimeSpan.FromMinutes(3), async () =>
        {
            if ((await _container.ExecAsync(["test", "-s", alignedPath])).ExitCode == 0)
            {
                return "done";
            }

            var log = await GetPluginLogAsync();
            if (FailureMarkers.Any(marker => log.Contains(marker, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The plugin reported an alignment failure:" + Environment.NewLine + log);
            }

            return null;
        });

        var aligned = Encoding.UTF8.GetString(await _container.ReadFileAsync(alignedPath));
        var expected = SrtTimings.CueStarts(original);
        var actual = SrtTimings.CueStarts(aligned);
        Assert.Equal(expected.Count, actual.Count);
        var worst = expected.Zip(actual, (e, a) => (a - e).Duration()).Max();
        Assert.True(worst <= Tolerance, $"Aligned cue starts differ from the originals by up to {worst.TotalSeconds:F3}s.");
    }

    private async Task<string> CompleteSetupAndLogInAsync()
    {
        await ApiAsync("POST", "/Startup/Configuration", null, """{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}""");
        await ApiAsync("GET", "/Startup/User", null);
        await ApiAsync("POST", "/Startup/User", null, """{"Name":"tests","Password":"tests"}""");
        await ApiAsync("POST", "/Startup/Complete", null);

        using var auth = JsonDocument.Parse(await ApiAsync("POST", "/Users/AuthenticateByName", null, """{"Username":"tests","Pw":"tests"}"""));
        return auth.RootElement.GetProperty("AccessToken").GetString()!;
    }

    private async Task<string> ApiAsync(string method, string path, string? token, string? jsonBody = null)
    {
        var authorization = token is null ? ClientAuthorization : $"{ClientAuthorization}, Token=\"{token}\"";
        var command = new[]
        {
            "curl", "-sS", "-X", method, "-w", "\n%{http_code}",
            "-H", "Content-Type: application/json",
            "-H", "Authorization: " + authorization,
            "-d", jsonBody ?? string.Empty,
            "http://localhost:8096" + path,
        };

        var result = await _container.ExecAsync(command);
        var output = result.Stdout.TrimEnd();
        var separator = output.LastIndexOf('\n');
        var status = output[(separator + 1)..];
        var body = separator < 0 ? string.Empty : output[..separator];

        if (result.ExitCode != 0 || !status.StartsWith('2'))
        {
            throw new InvalidOperationException($"{method} {path} failed (curl exit {result.ExitCode}, HTTP {status}): {body}{result.Stderr}");
        }

        return body;
    }

    private async Task<string> WaitForAsync(string what, TimeSpan timeout, Func<Task<string?>> probe)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"Timed out after {timeout} waiting for {what}. Log:{Environment.NewLine}{await GetPluginLogAsync()}");
    }

    /// <summary>
    /// The container log from the plugin's first line onwards, so multi-line
    /// messages such as ffsubsync's stderr stay intact.
    /// </summary>
    private async Task<string> GetPluginLogAsync()
    {
        var (stdout, stderr) = await _container.GetLogsAsync();
        var log = stdout + stderr;
        var start = log.IndexOf("SubtitleAutoAlign", StringComparison.Ordinal);
        return start < 0 ? log : log[Math.Max(0, log.LastIndexOf('\n', start) + 1)..];
    }
}
