namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public sealed class MediaMatchResult
{
    public bool IsMatch { get; set; }

    public bool IsAmbiguous { get; set; }

    public string JellyfinItemId { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string FailureReason { get; set; } = string.Empty;
}
