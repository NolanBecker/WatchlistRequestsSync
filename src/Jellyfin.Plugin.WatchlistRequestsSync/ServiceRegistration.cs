using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WatchlistRequestsSync;

public sealed class ServiceRegistration : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IJellyfinApi, JellyfinApi>();
        serviceCollection.AddHttpClient<ISeerrClient, SeerrClient>();
        serviceCollection.AddSingleton<IPluginConfigurationAccessor, PluginConfigurationAccessor>();
        serviceCollection.AddSingleton<IPluginStateStore, JsonPluginStateStore>();
        serviceCollection.AddSingleton<IUserMappingService, UserMappingService>();
        serviceCollection.AddSingleton<ITagSyncService, TagSyncService>();
        serviceCollection.AddSingleton<IJellyfinMediaMatcher, JellyfinMediaMatcher>();
        serviceCollection.AddSingleton<IKefinTweaksWatchlistAdapter, KefinTweaksWatchlistAdapter>();
        serviceCollection.AddSingleton<ISyncService, SyncService>();
    }
}
