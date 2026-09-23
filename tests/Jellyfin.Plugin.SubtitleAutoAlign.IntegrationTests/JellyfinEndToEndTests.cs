using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Jellyfin.Plugin.SubtitleAutoAlign.ScheduledTasks;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Runs Jellyfin 12.1 with the plugin (including the bundled ffsubsync) and the
/// "Us Now" clip in /media. Each test class gets its own container.
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

/// <summary>
/// A newly-appeared subtitle is aligned automatically.
/// </summary>
[Trait("Category", "JellyfinContainer")]
public class JellyfinEndToEndTests : IClassFixture<JellyfinEndToEndFixture>
{
    private readonly JellyfinTestServer _server;

    public JellyfinEndToEndTests(JellyfinEndToEndFixture fixture)
    {
        _server = new JellyfinTestServer(fixture.Container);
    }

    [Fact]
    public async Task NewSubtitle_IsAutoAlignedNextToTheMovie()
    {
        await _server.CompleteSetupAndLogInAsync();
        var movieId = await _server.CreateMovieLibraryAsync();

        // A downloaded subtitle lands next to the movie and the item gets refreshed.
        await _server.AddShiftedSubtitleAsync(movieId);

        await _server.AssertAlignedSubtitleRestoresOriginalAsync();
    }
}

/// <summary>
/// With automatic alignment off, the "Align all subtitles" task still aligns
/// subtitles that are already in the library.
/// </summary>
[Trait("Category", "JellyfinContainer")]
public class JellyfinAlignAllTaskTests : IClassFixture<JellyfinEndToEndFixture>
{
    private readonly JellyfinTestServer _server;

    public JellyfinAlignAllTaskTests(JellyfinEndToEndFixture fixture)
    {
        _server = new JellyfinTestServer(fixture.Container);
    }

    [Fact]
    public async Task AlignAllTask_AlignsExistingSubtitles()
    {
        await _server.CompleteSetupAndLogInAsync();
        await _server.SetAutoAlignAsync(false);
        var movieId = await _server.CreateMovieLibraryAsync();
        await _server.AddShiftedSubtitleAsync(movieId);
        await _server.WaitForExternalSubtitleAsync(movieId);

        Assert.False(await _server.AlignedSubtitleExistsAsync(), "Automatic alignment is off, so nothing should be aligned yet.");

        await _server.StartScheduledTaskAsync(AlignAllSubtitlesTask.TaskKey);

        await _server.AssertAlignedSubtitleRestoresOriginalAsync();
    }
}

/// <summary>
/// Drives a Jellyfin test container through its HTTP API, calling curl
/// inside the container so no host port is needed.
/// </summary>
public sealed class JellyfinTestServer
{
    private const string ClientAuthorization = "MediaBrowser Client=\"integration-tests\", Device=\"integration-tests\", DeviceId=\"integration-tests\", Version=\"1.0.0\"";
    private const string AlignedPath = JellyfinEndToEndFixture.MovieDirectory + "/us-now-5min.en.autoaligned.srt";
    private static readonly TimeSpan Shift = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(1);

    // Warnings SubtitleAlignmentService logs when an alignment attempt fails.
    private static readonly string[] FailureMarkers = ["ffsubsync exited with code", "Failed to run ffsubsync", "ffsubsync timed out"];

    private readonly IContainer _container;
    private string? _token;

    public JellyfinTestServer(IContainer container)
    {
        _container = container;
    }

    private static string OriginalSubtitle =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "us-now-5min.en.srt"));

    public async Task CompleteSetupAndLogInAsync()
    {
        await ApiAsync("POST", "/Startup/Configuration", """{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}""");
        await ApiAsync("GET", "/Startup/User");
        await ApiAsync("POST", "/Startup/User", """{"Name":"tests","Password":"tests"}""");
        await ApiAsync("POST", "/Startup/Complete");

        using var auth = JsonDocument.Parse(await ApiAsync("POST", "/Users/AuthenticateByName", """{"Username":"tests","Pw":"tests"}"""));
        _token = auth.RootElement.GetProperty("AccessToken").GetString();
    }

    /// <summary>Creates a Movies library over /media and returns the clip's item id.</summary>
    public async Task<string> CreateMovieLibraryAsync()
    {
        await ApiAsync("POST", "/Library/VirtualFolders?name=Movies&collectionType=movies&refreshLibrary=true",
            """{"LibraryOptions":{"PathInfos":[{"Path":"/media"}]}}""");

        return await WaitForAsync("the movie to be added to the library", TimeSpan.FromMinutes(2), async () =>
        {
            using var items = JsonDocument.Parse(await ApiAsync("GET", "/Items?Recursive=true&IncludeItemTypes=Movie"));
            return items.RootElement.GetProperty("Items").EnumerateArray().Select(i => i.GetProperty("Id").GetString()).FirstOrDefault();
        });
    }

    public async Task SetAutoAlignAsync(bool enabled)
    {
        var path = $"/Plugins/{Plugin.PluginGuid}/Configuration";
        var configuration = JsonNode.Parse(await ApiAsync("GET", path))!;
        configuration["EnableAutoAlign"] = enabled;
        await ApiAsync("POST", path, configuration.ToJsonString());
    }

    /// <summary>Puts the subtitle, shifted by 6 s, next to the movie and refreshes the item.</summary>
    public async Task AddShiftedSubtitleAsync(string movieId)
    {
        await _container.CopyAsync(
            Encoding.UTF8.GetBytes(SrtTimings.Shift(OriginalSubtitle, Shift)),
            $"{JellyfinEndToEndFixture.MovieDirectory}/us-now-5min.en.srt");
        await ApiAsync("POST", $"/Items/{movieId}/Refresh?metadataRefreshMode=Default&imageRefreshMode=Default");
    }

    public Task WaitForExternalSubtitleAsync(string movieId)
    {
        return WaitForAsync("Jellyfin to pick up the external subtitle", TimeSpan.FromMinutes(1), async () =>
        {
            using var items = JsonDocument.Parse(await ApiAsync("GET", $"/Items?Ids={movieId}&Fields=MediaStreams"));
            var hasExternalSubtitle = items.RootElement.GetProperty("Items").EnumerateArray()
                .SelectMany(i => i.GetProperty("MediaStreams").EnumerateArray())
                .Any(s => s.GetProperty("Type").GetString() == "Subtitle" && s.GetProperty("IsExternal").GetBoolean());
            return hasExternalSubtitle ? "done" : null;
        });
    }

    public async Task StartScheduledTaskAsync(string key)
    {
        using var tasks = JsonDocument.Parse(await ApiAsync("GET", "/ScheduledTasks"));
        var id = tasks.RootElement.EnumerateArray()
            .Where(t => t.GetProperty("Key").GetString() == key)
            .Select(t => t.GetProperty("Id").GetString())
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Scheduled task '{key}' is not registered.");

        await ApiAsync("POST", $"/ScheduledTasks/Running/{id}");
    }

    public async Task<bool> AlignedSubtitleExistsAsync() =>
        (await _container.ExecAsync(["test", "-s", AlignedPath])).ExitCode == 0;

    /// <summary>Waits for the aligned file and checks it is within 1 s of the unshifted original.</summary>
    public async Task AssertAlignedSubtitleRestoresOriginalAsync()
    {
        await WaitForAsync("the plugin to write " + AlignedPath, TimeSpan.FromMinutes(3), async () =>
        {
            if (await AlignedSubtitleExistsAsync())
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

        var aligned = Encoding.UTF8.GetString(await _container.ReadFileAsync(AlignedPath));
        var expected = SrtTimings.CueStarts(OriginalSubtitle);
        var actual = SrtTimings.CueStarts(aligned);
        Assert.Equal(expected.Count, actual.Count);
        var worst = expected.Zip(actual, (e, a) => (a - e).Duration()).Max();
        Assert.True(worst <= Tolerance, $"Aligned cue starts differ from the originals by up to {worst.TotalSeconds:F3}s.");
    }

    private async Task<string> ApiAsync(string method, string path, string? jsonBody = null)
    {
        var authorization = _token is null ? ClientAuthorization : $"{ClientAuthorization}, Token=\"{_token}\"";
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
