using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Publishes the plugin, installs it into a real Jellyfin 12.1 container and
/// captures the server log. Requires a Docker daemon on the machine running
/// the tests, so these run on the host, not inside Dockerfile.testrunner.
/// </summary>
public sealed class JellyfinContainerFixture : IAsyncLifetime
{
    public const string PluginVersion = "1.0.0.0";
    public const string PluginFolder = "Subtitle Auto Align_" + PluginVersion;

    private const string Image = "jellyfin/jellyfin:12.1";
    private const string WatcherStartedMessage = "Subtitle Auto Align is watching the library";

    private readonly string _pluginDirectory = Directory.CreateTempSubdirectory("subtitle-auto-align-plugin-").FullName;
    private IContainer? _container;

    public string Logs { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await PublishPluginAsync(_pluginDirectory);
        WriteManifest(_pluginDirectory);

        // Ryuk needs a published host port, which firewalld/nftables setups
        // often block, making startup hang. This test only reads container
        // logs, and DisposeAsync removes the container itself.
        TestcontainersSettings.ResourceReaperEnabled = false;

        // Copied in rather than bind-mounted: SELinux would block the
        // container from reading an unlabelled host directory.
        _container = new ContainerBuilder(Image)
            .WithResourceMapping(new DirectoryInfo(_pluginDirectory), $"/config/plugins/{PluginFolder}/")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Startup complete"))
            .Build();

        await _container.StartAsync();

        // Hosted services start just before "Startup complete", but give the
        // log a few seconds to flush so the assertions see the full startup.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        do
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            Logs = stdout + stderr;
            if (Logs.Contains(WatcherStartedMessage, StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        while (DateTime.UtcNow < deadline);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        Directory.Delete(_pluginDirectory, recursive: true);
    }

    private static async Task PublishPluginAsync(string outputDirectory)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "src", "Jellyfin.Plugin.SubtitleAutoAlign");

        var result = await new ProcessRunner().RunAsync(
            "dotnet",
            ["publish", projectPath, "-c", "Release", $"-p:Version={PluginVersion}", "-o", outputDirectory],
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
        }
    }

    private static void WriteManifest(string pluginDirectory)
    {
        var manifest = new
        {
            guid = Plugin.PluginGuid.ToString(),
            name = "Subtitle Auto Align",
            description = "Integration test install.",
            overview = "Integration test install.",
            owner = "integration-tests",
            category = "Subtitles",
            version = PluginVersion,
            targetAbi = "12.1.0.0",
            changelog = string.Empty,
            timestamp = DateTime.UtcNow.ToString("O"),
            status = "Active",
            autoUpdate = false,
            imagePath = string.Empty,
            assemblies = Array.Empty<string>(),
        };

        File.WriteAllText(Path.Combine(pluginDirectory, "meta.json"), JsonSerializer.Serialize(manifest));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SubtitleAutoAlign.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find SubtitleAutoAlign.sln above " + AppContext.BaseDirectory);
    }
}

[Trait("Category", "JellyfinContainer")]
public class JellyfinPluginLoadTests : IClassFixture<JellyfinContainerFixture>
{
    private readonly JellyfinContainerFixture _fixture;

    public JellyfinPluginLoadTests(JellyfinContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Plugin_IsLoadedByJellyfin()
    {
        // Console output renders the name unquoted, the log file quotes it.
        Assert.Matches("Loaded plugin: \"?Subtitle Auto Align\"? \"?" + Regex.Escape(JellyfinContainerFixture.PluginVersion), _fixture.Logs);
    }

    [Fact]
    public void Plugin_DoesNotBundleServerAssemblies()
    {
        // A bundled copy of MediaBrowser.* gets loaded into the plugin's own
        // load context, so its IPlugin/IPluginServiceRegistrator types don't
        // match the server's and the plugin is silently ignored.
        Assert.DoesNotContain(
            $"/config/plugins/{JellyfinContainerFixture.PluginFolder}/MediaBrowser.",
            _fixture.Logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Plugin_LoadsWithoutErrors()
    {
        Assert.DoesNotContain("Error creating Jellyfin.Plugin.SubtitleAutoAlign", _fixture.Logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Error registering plugin services from Jellyfin.Plugin.SubtitleAutoAlign", _fixture.Logs, StringComparison.Ordinal);
        Assert.DoesNotMatch($"Failed to load assembly \"?/config/plugins/{Regex.Escape(JellyfinContainerFixture.PluginFolder)}", _fixture.Logs);
    }

    [Fact]
    public void SubtitleWatcher_StartsWithServer()
    {
        Assert.Contains("Subtitle Auto Align is watching the library", _fixture.Logs, StringComparison.Ordinal);
    }
}
