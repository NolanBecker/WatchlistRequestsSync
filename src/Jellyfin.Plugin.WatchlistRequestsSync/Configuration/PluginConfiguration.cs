using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool IsEnabled { get; set; } = true;

    public string SonarrBaseUrl { get; set; } = string.Empty;

    public string SonarrApiKey { get; set; } = string.Empty;

    public string SonarrTags { get; set; } = string.Empty;

    public string RadarrBaseUrl { get; set; } = string.Empty;

    public string RadarrApiKey { get; set; } = string.Empty;

    public string RadarrTags { get; set; } = string.Empty;

    public int SyncIntervalMinutes { get; set; } = 360;

    public bool DryRun { get; set; }

    public LogVerbosity LoggingLevel { get; set; } = LogVerbosity.Information;

    public List<UserSyncSettings> Users { get; set; } = [];
}
