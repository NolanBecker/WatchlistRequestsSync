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

public interface IArrClient
{
    ArrSourceKind Source { get; }

    Task<ArrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);

    Task<ArrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(string baseUrl, string apiKey, string configuredTags, CancellationToken cancellationToken);
}

public interface IJellyfinMediaMatcher
{
    Task<MediaMatchResult> MatchItemAsync(string jellyfinUserId, ArrMediaItem item, CancellationToken cancellationToken);
}

public interface IKefinTweaksWatchlistAdapter
{
    Task<CompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken);

    Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken);
}

public interface ISyncService
{
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);

    Task<ConnectionTestResult> TestConnectionAsync(ConnectionTestRequest request, CancellationToken cancellationToken);

    Task<SyncExecutionResult> PreviewAsync(CancellationToken cancellationToken);

    Task<SyncExecutionResult> PreviewAsync(PluginConfiguration configuration, CancellationToken cancellationToken);

    Task<SyncExecutionResult> RunAsync(SyncRunMode mode, CancellationToken cancellationToken);

    Task<SyncExecutionResult> RunAsync(SyncRunMode mode, PluginConfiguration configuration, CancellationToken cancellationToken);
}
