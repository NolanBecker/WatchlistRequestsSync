namespace Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

public enum SyncRunMode
{
    Scheduled,
    Manual,
    Preview
}

public enum LogVerbosity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error
}

public enum PartialAvailabilityMode
{
    FollowAvailableToggle,
    Exclude,
    Include
}
