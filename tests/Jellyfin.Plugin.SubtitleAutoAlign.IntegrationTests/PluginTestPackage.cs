using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Builds what a Jellyfin install of the plugin needs: the published plugin
/// with a meta.json, and the bundled ffsubsync binary.
/// </summary>
public static class PluginTestPackage
{
    public const string PluginVersion = "1.0.0.0";
    public const string PluginFolder = "Subtitle Auto Align_" + PluginVersion;
    public const string PluginPath = "/config/plugins/" + PluginFolder;
    public const string JellyfinImage = "jellyfin/jellyfin:12.1";

    private static readonly Lazy<Task<byte[]>> FfSubSyncBinary = new(BuildFfSubSyncAsync);

    static PluginTestPackage()
    {
        // Ryuk needs a published host port, which firewalld/nftables setups
        // often block, making container startup hang. Fixtures dispose their
        // own containers instead.
        TestcontainersSettings.ResourceReaperEnabled = false;
    }

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Publishes the plugin into <paramref name="outputDirectory"/> and adds its meta.json.</summary>
    public static async Task PublishAsync(string outputDirectory)
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Jellyfin.Plugin.SubtitleAutoAlign");

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

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "meta.json"), JsonSerializer.Serialize(manifest));
    }

    /// <summary>
    /// The linux-x64 ffsubsync binary, built once per test run from
    /// build/ffsubsync/Dockerfile — the same recipe the release workflow uses.
    /// </summary>
    public static Task<byte[]> GetFfSubSyncBinaryAsync() => FfSubSyncBinary.Value;

    private static async Task<byte[]> BuildFfSubSyncAsync()
    {
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(Path.Combine(RepositoryRoot, "build", "ffsubsync"))
            .WithDockerfile("Dockerfile")
            .WithName("subtitle-auto-align/ffsubsync-build:test")
            .WithCleanUp(false)
            .Build();
        await image.CreateAsync();

        await using var container = new ContainerBuilder(image)
            .WithEntrypoint("sleep")
            .WithCommand("infinity")
            .Build();
        await container.StartAsync();

        return await container.ReadFileAsync("/build/dist/ffsubsync");
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
