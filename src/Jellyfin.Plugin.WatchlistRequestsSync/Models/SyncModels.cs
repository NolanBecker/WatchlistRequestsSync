namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public enum SyncItemSource
{
    Request,
    Tag
}

public sealed class WatchlistMetadataEntry
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinItemId { get; set; } = string.Empty;

    public SyncItemSource Source { get; set; }

    public long? RequestId { get; set; }

    public ProviderIdSet ProviderIds { get; set; } = new();

    public DateTimeOffset AddedAtUtc { get; set; }
}

public sealed class PluginState
{
    public DateTimeOffset? LastSyncUtc { get; set; }

    public List<WatchlistMetadataEntry> Entries { get; set; } = [];
}

public sealed class SyncCandidate
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinItemId { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public SyncItemSource Source { get; set; }

    public long? RequestId { get; set; }

    public ProviderIdSet ProviderIds { get; set; } = new();
}

public sealed class UnmatchedItemReport
{
    public string SourceName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public ProviderIdSet ProviderIds { get; set; } = new();
}

public sealed class PerUserSyncReport
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinUserName { get; set; } = string.Empty;

    public string SeerrUserId { get; set; } = string.Empty;

    public List<SyncCandidate> RequestCandidates { get; set; } = [];

    public List<SyncCandidate> TagCandidates { get; set; } = [];

    public List<SyncCandidate> AddedItems { get; set; } = [];

    public List<SyncCandidate> SkippedDuplicates { get; set; } = [];

    public List<UnmatchedItemReport> UnmatchedItems { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}

public sealed class SyncExecutionResult
{
    public bool IsDryRun { get; set; }

    public bool CompatibilityOk { get; set; }

    public string CompatibilityMessage { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PerUserSyncReport> Users { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}
