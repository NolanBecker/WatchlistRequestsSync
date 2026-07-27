namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public sealed class UserSyncSettings
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinUserName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IncludeMovies { get; set; } = true;

    public bool IncludeSeries { get; set; } = true;
}
