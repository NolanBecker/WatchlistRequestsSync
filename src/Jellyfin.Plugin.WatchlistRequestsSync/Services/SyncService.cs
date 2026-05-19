using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class SyncService : ISyncService
{
    private readonly IPluginConfigurationAccessor _configurationAccessor;
    private readonly IPluginStateStore _stateStore;
    private readonly ISeerrClient _seerrClient;
    private readonly IUserMappingService _userMappingService;
    private readonly IJellyfinMediaMatcher _mediaMatcher;
    private readonly IKefinTweaksWatchlistAdapter _watchlistAdapter;
    private readonly ITagSyncService _tagSyncService;

    public SyncService(
        IPluginConfigurationAccessor configurationAccessor,
        IPluginStateStore stateStore,
        ISeerrClient seerrClient,
        IUserMappingService userMappingService,
        IJellyfinMediaMatcher mediaMatcher,
        IKefinTweaksWatchlistAdapter watchlistAdapter,
        ITagSyncService tagSyncService)
    {
        _configurationAccessor = configurationAccessor;
        _stateStore = stateStore;
        _seerrClient = seerrClient;
        _userMappingService = userMappingService;
        _mediaMatcher = mediaMatcher;
        _watchlistAdapter = watchlistAdapter;
        _tagSyncService = tagSyncService;
    }

    public Task<SeerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
        => _seerrClient.TestConnectionAsync(cancellationToken);

    public Task<SeerrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
        => _seerrClient.TestConnectionAsync(baseUrl, apiKey, cancellationToken);

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

        IReadOnlyList<NormalizedSeerrRequest> requests;
        try
        {
            requests = await _seerrClient.GetRequestsAsync(configuration.SeerrBaseUrl, configuration.ApiKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to fetch requests: {ex.Message}");
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            return result;
        }

        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var explicitMappings = configuration.Users
            .Where(static u => long.TryParse(u.SeerrUserId, out _))
            .ToDictionary(static u => long.Parse(u.SeerrUserId), static u => u.JellyfinUserId);

        foreach (var userSettings in configuration.Users.Where(static user => user.IsEnabled))
        {
            var perUser = new PerUserSyncReport
            {
                JellyfinUserId = userSettings.JellyfinUserId,
                JellyfinUserName = userSettings.JellyfinUserName,
                SeerrUserId = userSettings.SeerrUserId
            };

            var watchlistItems = await _watchlistAdapter.GetWatchlistItemIdsAsync(userSettings.JellyfinUserId, cancellationToken).ConfigureAwait(false);
            var requestCandidates = await GetRequestCandidatesAsync(userSettings, configuration, requests, explicitMappings, perUser, cancellationToken).ConfigureAwait(false);
            var tagCandidates = await _tagSyncService.GetTagCandidatesAsync(userSettings, cancellationToken).ConfigureAwait(false);

            perUser.RequestCandidates.AddRange(requestCandidates);
            perUser.TagCandidates.AddRange(tagCandidates);

            foreach (var candidate in requestCandidates.Concat(tagCandidates).DistinctBy(static c => c.JellyfinItemId))
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
                        RequestId = candidate.RequestId,
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

    private async Task<IReadOnlyList<SyncCandidate>> GetRequestCandidatesAsync(
        UserSyncSettings userSettings,
        PluginConfiguration configuration,
        IReadOnlyList<NormalizedSeerrRequest> allRequests,
        IReadOnlyDictionary<long, string> explicitMappings,
        PerUserSyncReport perUser,
        CancellationToken cancellationToken)
    {
        var includedRequests = allRequests
            .Where(request => string.Equals(_userMappingService.MapSeerrRequestToJellyfinUser(request, explicitMappings), userSettings.JellyfinUserId, StringComparison.OrdinalIgnoreCase))
            .Where(request => ShouldIncludeRequest(configuration, userSettings, request))
            .ToList();

        var candidates = new List<SyncCandidate>();
        foreach (var request in includedRequests)
        {
            var match = await _mediaMatcher.MatchRequestAsync(userSettings.JellyfinUserId, request, cancellationToken).ConfigureAwait(false);
            if (!match.IsMatch)
            {
                perUser.UnmatchedItems.Add(new UnmatchedItemReport
                {
                    SourceName = request.Title,
                    Reason = match.FailureReason,
                    ProviderIds = request.ProviderIds
                });
                continue;
            }

            candidates.Add(new SyncCandidate
            {
                JellyfinUserId = userSettings.JellyfinUserId,
                JellyfinItemId = match.JellyfinItemId,
                ItemName = match.ItemName,
                Source = SyncItemSource.Request,
                RequestId = request.RequestId,
                ProviderIds = request.ProviderIds
            });
        }

        return candidates;
    }

    private static bool ShouldIncludeRequest(PluginConfiguration configuration, UserSyncSettings userSettings, NormalizedSeerrRequest request)
    {
        if (request.MediaType == RequestMediaType.Movie && !userSettings.IncludeMovies)
        {
            return false;
        }

        if (request.MediaType == RequestMediaType.Series && !userSettings.IncludeSeries)
        {
            return false;
        }

        if (request.ApprovalState == RequestApprovalState.Pending && !userSettings.IncludePendingRequests)
        {
            return false;
        }

        if (request.ApprovalState == RequestApprovalState.Approved && !userSettings.IncludeApprovedRequests)
        {
            return false;
        }

        if (request.AvailabilityState == RequestAvailabilityState.Available && !userSettings.IncludeAvailableRequests)
        {
            return false;
        }

        var partialMode = configuration.PartialAvailabilityMode;
        if (request.AvailabilityState == RequestAvailabilityState.PartiallyAvailable)
        {
            return partialMode switch
            {
                PartialAvailabilityMode.Include => true,
                PartialAvailabilityMode.Exclude => false,
                _ => userSettings.IncludeAvailableRequests
            };
        }

        return true;
    }
}
