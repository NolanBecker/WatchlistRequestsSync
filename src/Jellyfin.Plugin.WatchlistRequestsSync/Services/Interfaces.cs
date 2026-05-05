using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public interface IPluginConfigurationAccessor
{
    PluginConfiguration GetConfiguration();
}

public interface IPluginStateStore
{
    Task<PluginState> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(PluginState state, CancellationToken cancellationToken);
}

public interface ISeerrClient
{
    Task<SeerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);

    Task<SeerrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedSeerrRequest>> GetRequestsAsync(CancellationToken cancellationToken);
}

public interface IUserMappingService
{
    string? MapSeerrRequestToJellyfinUser(NormalizedSeerrRequest request, IReadOnlyDictionary<long, string> explicitMappings);
}

public interface ITagSyncService
{
    Task<IReadOnlyList<SyncCandidate>> GetTagCandidatesAsync(UserSyncSettings userSettings, CancellationToken cancellationToken);
}

public interface IJellyfinMediaMatcher
{
    Task<MediaMatchResult> MatchRequestAsync(string jellyfinUserId, NormalizedSeerrRequest request, CancellationToken cancellationToken);
}

public interface IKefinTweaksWatchlistAdapter
{
    Task<CompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken);

    Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken);
}

public interface ISyncService
{
    Task<SeerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);

    Task<SeerrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken);

    Task<SyncExecutionResult> PreviewAsync(CancellationToken cancellationToken);

    Task<SyncExecutionResult> RunAsync(SyncRunMode mode, CancellationToken cancellationToken);
}
