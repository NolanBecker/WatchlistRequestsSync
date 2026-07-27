using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class SyncService : ISyncService
{
    private readonly IPluginConfigurationAccessor _configurationAccessor;
    private readonly IPluginStateStore _stateStore;
    private readonly IReadOnlyList<IArrClient> _arrClients;
    private readonly IJellyfinMediaMatcher _mediaMatcher;
    private readonly IKefinTweaksWatchlistAdapter _watchlistAdapter;

    public SyncService(
        IPluginConfigurationAccessor configurationAccessor,
        IPluginStateStore stateStore,
        IEnumerable<IArrClient> arrClients,
        IJellyfinMediaMatcher mediaMatcher,
        IKefinTweaksWatchlistAdapter watchlistAdapter)
    {
        _configurationAccessor = configurationAccessor;
        _stateStore = stateStore;
        _arrClients = arrClients.ToList();
        _mediaMatcher = mediaMatcher;
        _watchlistAdapter = watchlistAdapter;
    }

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
        => TestConnectionAsync(ToConnectionTestRequest(_configurationAccessor.GetConfiguration()), cancellationToken);

    public async Task<ConnectionTestResult> TestConnectionAsync(ConnectionTestRequest request, CancellationToken cancellationToken)
    {
        var result = new ConnectionTestResult();
        foreach (var client in _arrClients)
        {
            var (baseUrl, apiKey) = GetConnectionSettings(client.Source, request);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                result.Sources.Add(new SourceConnectionResult
                {
                    Source = client.Source,
                    IsEnabled = false,
                    IsSuccess = true,
                    Message = "Not configured."
                });
                continue;
            }

            var sourceResult = await client.TestConnectionAsync(baseUrl, apiKey, cancellationToken).ConfigureAwait(false);
            result.Sources.Add(new SourceConnectionResult
            {
                Source = client.Source,
                IsEnabled = true,
                IsSuccess = sourceResult.IsSuccess,
                Message = sourceResult.Message,
                NormalizedBaseUrl = sourceResult.NormalizedBaseUrl
            });
        }

        var enabledSources = result.Sources.Where(static source => source.IsEnabled).ToList();
        result.IsSuccess = enabledSources.Count > 0 && enabledSources.All(static source => source.IsSuccess);
        return result;
    }

    public Task<SyncExecutionResult> PreviewAsync(CancellationToken cancellationToken)
        => RunCoreAsync(SyncRunMode.Preview, _configurationAccessor.GetConfiguration(), true, cancellationToken);

    public Task<SyncExecutionResult> PreviewAsync(PluginConfiguration configuration, CancellationToken cancellationToken)
        => RunCoreAsync(SyncRunMode.Preview, configuration, true, cancellationToken);

    public Task<SyncExecutionResult> RunAsync(SyncRunMode mode, CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        return RunCoreAsync(mode, configuration, configuration.DryRun || mode == SyncRunMode.Preview, cancellationToken);
    }

    public Task<SyncExecutionResult> RunAsync(SyncRunMode mode, PluginConfiguration configuration, CancellationToken cancellationToken)
        => RunCoreAsync(mode, configuration, configuration.DryRun || mode == SyncRunMode.Preview, cancellationToken);

    private async Task<SyncExecutionResult> RunCoreAsync(SyncRunMode mode, PluginConfiguration configuration, bool dryRun, CancellationToken cancellationToken)
    {
        var result = new SyncExecutionResult
        {
            IsDryRun = dryRun,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        if (!configuration.IsEnabled && mode != SyncRunMode.Preview)
        {
            result.Errors.Add("Plugin is disabled.");
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            return result;
        }

        var compatibility = await _watchlistAdapter.CheckCompatibilityAsync(cancellationToken).ConfigureAwait(false);
        result.CompatibilityOk = compatibility.IsCompatible;
        result.CompatibilityMessage = compatibility.Message;
        if (!compatibility.IsCompatible)
        {
            result.Errors.Add(compatibility.Message);
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            return result;
        }

        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var sourceItems = await LoadSourceItemsAsync(configuration, result, cancellationToken).ConfigureAwait(false);
        if (sourceItems.Count == 0)
        {
            result.Errors.Add("No tagged Sonarr or Radarr items were found. Check the configured base URLs, API keys, and tag names.");
        }

        var enabledUsers = configuration.Users.Where(static user => user.IsEnabled).ToList();
        if (enabledUsers.Count == 0)
        {
            result.Errors.Add("No Jellyfin users are enabled for sync. Enable at least one user in Per-User Settings.");
        }

        foreach (var userSettings in enabledUsers)
        {
            var perUser = new PerUserSyncReport
            {
                JellyfinUserId = userSettings.JellyfinUserId,
                JellyfinUserName = userSettings.JellyfinUserName
            };

            var watchlistItems = await _watchlistAdapter.GetWatchlistItemIdsAsync(userSettings.JellyfinUserId, cancellationToken).ConfigureAwait(false);
            var candidates = await GetCandidatesAsync(userSettings, sourceItems, perUser, cancellationToken).ConfigureAwait(false);
            perUser.CandidateItems.AddRange(candidates);

            foreach (var candidate in candidates.DistinctBy(static c => c.JellyfinItemId))
            {
                if (watchlistItems.Contains(candidate.JellyfinItemId))
                {
                    perUser.SkippedDuplicates.Add(candidate);
                    continue;
                }

                if (!dryRun)
                {
                    await _watchlistAdapter.AddToWatchlistAsync(userSettings.JellyfinUserId, candidate, cancellationToken).ConfigureAwait(false);
                    state.Entries.Add(new WatchlistMetadataEntry
                    {
                        JellyfinUserId = userSettings.JellyfinUserId,
                        JellyfinItemId = candidate.JellyfinItemId,
                        Source = candidate.Source,
                        SourceItemKey = candidate.SourceItemKey,
                        ProviderIds = candidate.ProviderIds,
                        AddedAtUtc = DateTimeOffset.UtcNow
                    });
                }

                perUser.AddedItems.Add(candidate);
            }

            result.Users.Add(perUser);
        }

        state.LastSyncUtc = DateTimeOffset.UtcNow;
        if (!dryRun)
        {
            await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }

        result.CompletedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task<IReadOnlyList<ArrMediaItem>> LoadSourceItemsAsync(
        PluginConfiguration configuration,
        SyncExecutionResult result,
        CancellationToken cancellationToken)
    {
        var items = new List<ArrMediaItem>();
        foreach (var client in _arrClients)
        {
            var (baseUrl, apiKey, tags) = GetSourceSettings(client.Source, configuration);
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(tags))
            {
                continue;
            }

            try
            {
                items.AddRange(await client.GetTaggedItemsAsync(baseUrl, apiKey, tags, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to fetch {client.Source} tagged items: {ex.Message}");
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<SyncCandidate>> GetCandidatesAsync(
        UserSyncSettings userSettings,
        IReadOnlyList<ArrMediaItem> sourceItems,
        PerUserSyncReport perUser,
        CancellationToken cancellationToken)
    {
        var includedItems = sourceItems
            .Where(item => ShouldIncludeItem(userSettings, item))
            .ToList();

        var candidates = new List<SyncCandidate>();
        foreach (var item in includedItems)
        {
            var match = await _mediaMatcher.MatchItemAsync(userSettings.JellyfinUserId, item, cancellationToken).ConfigureAwait(false);
            if (!match.IsMatch)
            {
                perUser.UnmatchedItems.Add(new UnmatchedItemReport
                {
                    SourceName = item.Title,
                    Reason = match.FailureReason,
                    ProviderIds = item.ProviderIds
                });
                continue;
            }

            candidates.Add(new SyncCandidate
            {
                JellyfinUserId = userSettings.JellyfinUserId,
                JellyfinItemId = match.JellyfinItemId,
                ItemName = match.ItemName,
                Source = item.Source == ArrSourceKind.Sonarr ? SyncItemSource.SonarrTag : SyncItemSource.RadarrTag,
                SourceItemKey = $"{item.Source}:{item.SourceItemId}",
                ProviderIds = item.ProviderIds
            });
        }

        return candidates;
    }

    private static bool ShouldIncludeItem(UserSyncSettings userSettings, ArrMediaItem item)
    {
        if (item.MediaKind == MediaKind.Movie && !userSettings.IncludeMovies)
        {
            return false;
        }

        if (item.MediaKind == MediaKind.Series && !userSettings.IncludeSeries)
        {
            return false;
        }

        return true;
    }

    private static ConnectionTestRequest ToConnectionTestRequest(PluginConfiguration configuration)
        => new()
        {
            SonarrBaseUrl = configuration.SonarrBaseUrl,
            SonarrApiKey = configuration.SonarrApiKey,
            RadarrBaseUrl = configuration.RadarrBaseUrl,
            RadarrApiKey = configuration.RadarrApiKey
        };

    private static (string BaseUrl, string ApiKey) GetConnectionSettings(ArrSourceKind source, ConnectionTestRequest request)
        => source switch
        {
            ArrSourceKind.Sonarr => (request.SonarrBaseUrl, request.SonarrApiKey),
            _ => (request.RadarrBaseUrl, request.RadarrApiKey)
        };

    private static (string BaseUrl, string ApiKey, string Tags) GetSourceSettings(ArrSourceKind source, PluginConfiguration configuration)
        => source switch
        {
            ArrSourceKind.Sonarr => (configuration.SonarrBaseUrl, configuration.SonarrApiKey, configuration.SonarrTags),
            _ => (configuration.RadarrBaseUrl, configuration.RadarrApiKey, configuration.RadarrTags)
        };
}
