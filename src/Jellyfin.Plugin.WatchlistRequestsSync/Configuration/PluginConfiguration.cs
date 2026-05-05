using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool IsEnabled { get; set; } = true;

    public string SeerrBaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int SyncIntervalMinutes { get; set; } = 360;

    public bool DryRun { get; set; }

    public LogVerbosity LoggingLevel { get; set; } = LogVerbosity.Information;

    public PartialAvailabilityMode PartialAvailabilityMode { get; set; } = PartialAvailabilityMode.FollowAvailableToggle;

    public RequestStatusOptions DefaultStatuses { get; set; } = new();

    public List<UserSyncSettings> Users { get; set; } = [];
}
