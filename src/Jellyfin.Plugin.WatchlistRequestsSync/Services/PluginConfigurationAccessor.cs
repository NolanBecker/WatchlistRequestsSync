using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class PluginConfigurationAccessor : IPluginConfigurationAccessor
{
    public PluginConfiguration GetConfiguration()
        => Plugin.Instance?.Configuration ?? new PluginConfiguration();
}
