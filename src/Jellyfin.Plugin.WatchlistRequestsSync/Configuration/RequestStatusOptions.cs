namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public sealed class RequestStatusOptions
{
    public bool IncludePending { get; set; } = true;

    public bool IncludeApproved { get; set; } = true;

    public bool IncludeAvailable { get; set; } = true;
}
