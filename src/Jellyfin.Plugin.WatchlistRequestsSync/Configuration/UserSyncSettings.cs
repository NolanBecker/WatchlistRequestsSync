namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public sealed class UserSyncSettings
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinUserName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string SeerrUserId { get; set; } = string.Empty;

    public bool PreferJellyfinUserIdMatch { get; set; } = true;

    public string MediaTag { get; set; } = string.Empty;

    public bool IncludeMovies { get; set; } = true;

    public bool IncludeSeries { get; set; } = true;

    public bool IncludePendingRequests { get; set; } = true;

    public bool IncludeApprovedRequests { get; set; } = true;

    public bool IncludeAvailableRequests { get; set; } = true;
}
