using System;
using System.IO;
using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Jellyfin.Plugin.SubtitleAutoAlign.EventSubscribers;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.SubtitleAutoAlign;

/// <inheritdoc />
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        WriteDiagnosticMarker("RegisterServices called");

        serviceCollection.AddSingleton<IProcessRunner, ProcessRunner>();
        serviceCollection.AddSingleton<IFfSubSyncLocator, FfSubSyncLocator>();
        serviceCollection.AddSingleton<ISubtitleAlignmentService>(provider =>
            new SubtitleAlignmentService(
                provider.GetRequiredService<IProcessRunner>(),
                provider.GetRequiredService<IFfSubSyncLocator>(),
                () => Plugin.Instance!.Configuration,
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SubtitleAlignmentService>>()));

        serviceCollection.AddHostedService<SubtitleDownloadWatcher>();
    }

    /// <summary>
    /// Writes directly to a fixed file, bypassing the logging pipeline entirely
    /// (log levels, categories, DI). Temporary diagnostic to determine whether
    /// this method is actually invoked by the Jellyfin server, independent of
    /// whether ILogger output is visible. Safe to remove once confirmed.
    /// </summary>
    private static void WriteDiagnosticMarker(string message)
    {
        try
        {
            File.AppendAllText(
                "/tmp/subtitle-auto-align-diagnostics.log",
                $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Best-effort diagnostic only; never let this affect real startup.
        }
    }
}
