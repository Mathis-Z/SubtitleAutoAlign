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
}
