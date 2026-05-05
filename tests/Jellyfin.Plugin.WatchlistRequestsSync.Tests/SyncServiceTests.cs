using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;
using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using Xunit;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Tests;

public sealed class SyncServiceTests
{
    [Fact]
    public void UserMapping_PrefersEmbeddedJellyfinUserId()
    {
        var service = new UserMappingService();
        var request = new NormalizedSeerrRequest
        {
            SeerrUserId = 33,
            JellyfinUserId = "user-a"
        };

        var result = service.MapSeerrRequestToJellyfinUser(request, new Dictionary<long, string> { [33] = "user-b" });

        Assert.Equal("user-a", result);
    }

    [Fact]
    public async Task DuplicatePrevention_SkipsAlreadyLikedItems()
    {
        var service = CreateSyncService(
            requests: [CreateRequest(101, 7, "user-a")],
            mediaMatchResults: new Dictionary<long, MediaMatchResult> { [101] = Match("item-1", "Inception") },
            watchlistItemIds: new HashSet<string> { "item-1" });

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.Single(result.Users);
        Assert.Empty(result.Users[0].AddedItems);
        Assert.Single(result.Users[0].SkippedDuplicates);
    }

    [Fact]
    public async Task DryRun_DoesNotWrite()
    {
        var fakeAdapter = new FakeWatchlistAdapter();
        var service = CreateSyncService(
            requests: [CreateRequest(101, 7, "user-a")],
            mediaMatchResults: new Dictionary<long, MediaMatchResult> { [101] = Match("item-1", "Inception") },
            adapter: fakeAdapter,
            configurationOverride: config => config.DryRun = true);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.True(result.IsDryRun);
        Assert.Empty(fakeAdapter.AddCalls);
        Assert.Single(result.Users[0].AddedItems);
    }

    [Fact]
    public async Task TagSync_IsPerUserOnly()
    {
        var tagService = new FakeTagSyncService(new Dictionary<string, IReadOnlyList<SyncCandidate>>
        {
            ["user-a"] = [new SyncCandidate { JellyfinUserId = "user-a", JellyfinItemId = "item-9", ItemName = "Tagged", Source = SyncItemSource.Tag }],
            ["user-b"] = [new SyncCandidate { JellyfinUserId = "user-b", JellyfinItemId = "item-8", ItemName = "Wrong User", Source = SyncItemSource.Tag }]
        });

        var service = CreateSyncService(
            requests: Array.Empty<NormalizedSeerrRequest>(),
            mediaMatchResults: new Dictionary<long, MediaMatchResult>(),
            tagSyncService: tagService);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.Single(result.Users[0].AddedItems);
        Assert.Equal("item-9", result.Users[0].AddedItems[0].JellyfinItemId);
    }

    [Fact]
    public async Task SeerrFailure_IsReportedWithoutWrites()
    {
        var fakeAdapter = new FakeWatchlistAdapter();
        var service = CreateSyncService(
            requestsException: new HttpRequestException("boom"),
            mediaMatchResults: new Dictionary<long, MediaMatchResult>(),
            adapter: fakeAdapter);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Empty(fakeAdapter.AddCalls);
    }

    [Fact]
    public async Task ProviderIdMatcher_PrefersProviderMatch()
    {
        var api = new FakeJellyfinApi
        {
            ProviderSearchResults =
            {
                [("user-a", RequestMediaType.Movie, "Tmdb", "27205")] =
                    [new JellyfinLibraryItem { Id = "movie-1", Name = "Inception", Type = "Movie" }]
            }
        };
        var matcher = new JellyfinMediaMatcher(api);

        var result = await matcher.MatchRequestAsync("user-a", new NormalizedSeerrRequest
        {
            MediaType = RequestMediaType.Movie,
            ProviderIds = new ProviderIdSet { Tmdb = "27205" },
            Title = "Inception",
            Year = 2010
        }, CancellationToken.None);

        Assert.True(result.IsMatch);
        Assert.Equal("movie-1", result.JellyfinItemId);
    }

    [Fact]
    public async Task TitleYearMatcher_SkipsAmbiguousFallback()
    {
        var api = new FakeJellyfinApi
        {
            TitleSearchResults =
            {
                [("user-a", RequestMediaType.Series, "Dark", 2017)] =
                [
                    new JellyfinLibraryItem { Id = "series-1", Name = "Dark", Type = "Series", ProductionYear = 2017 },
                    new JellyfinLibraryItem { Id = "series-2", Name = "Dark", Type = "Series", ProductionYear = 2017 }
                ]
            }
        };
        var matcher = new JellyfinMediaMatcher(api);

        var result = await matcher.MatchRequestAsync("user-a", new NormalizedSeerrRequest
        {
            MediaType = RequestMediaType.Series,
            Title = "Dark",
            Year = 2017
        }, CancellationToken.None);

        Assert.False(result.IsMatch);
        Assert.True(result.IsAmbiguous);
    }

    private static SyncService CreateSyncService(
        IReadOnlyList<NormalizedSeerrRequest>? requests = null,
        Dictionary<long, MediaMatchResult>? mediaMatchResults = null,
        HashSet<string>? watchlistItemIds = null,
        FakeWatchlistAdapter? adapter = null,
        FakeTagSyncService? tagSyncService = null,
        Exception? requestsException = null,
        Action<PluginConfiguration>? configurationOverride = null)
    {
        var configuration = new PluginConfiguration
        {
            Users =
            [
                new UserSyncSettings
                {
                    JellyfinUserId = "user-a",
                    JellyfinUserName = "User A",
                    IsEnabled = true,
                    SeerrUserId = "7",
                    MediaTag = "watchlist-a"
                }
            ]
        };
        configurationOverride?.Invoke(configuration);

        return new SyncService(
            new FakeConfigurationAccessor(configuration),
            new FakePluginStateStore(),
            new FakeSeerrClient(requests ?? Array.Empty<NormalizedSeerrRequest>(), requestsException),
            new UserMappingService(),
            new FakeMediaMatcher(mediaMatchResults ?? new Dictionary<long, MediaMatchResult>()),
            adapter ?? new FakeWatchlistAdapter(watchlistItemIds ?? []),
            tagSyncService ?? new FakeTagSyncService(new Dictionary<string, IReadOnlyList<SyncCandidate>>
            {
                ["user-a"] = Array.Empty<SyncCandidate>()
            }));
    }

    private static NormalizedSeerrRequest CreateRequest(long id, long seerrUserId, string jellyfinUserId)
        => new()
        {
            RequestId = id,
            SeerrUserId = seerrUserId,
            JellyfinUserId = jellyfinUserId,
            Title = "Inception",
            MediaType = RequestMediaType.Movie,
            ApprovalState = RequestApprovalState.Approved,
            AvailabilityState = RequestAvailabilityState.Pending,
            ProviderIds = new ProviderIdSet { Tmdb = "27205" }
        };

    private static MediaMatchResult Match(string itemId, string itemName)
        => new()
        {
            IsMatch = true,
            JellyfinItemId = itemId,
            ItemName = itemName
        };

    private sealed class FakeConfigurationAccessor : IPluginConfigurationAccessor
    {
        private readonly PluginConfiguration _configuration;

        public FakeConfigurationAccessor(PluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public PluginConfiguration GetConfiguration() => _configuration;
    }

    private sealed class FakePluginStateStore : IPluginStateStore
    {
        public PluginState State { get; private set; } = new();

        public Task<PluginState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        public Task SaveAsync(PluginState state, CancellationToken cancellationToken)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSeerrClient : ISeerrClient
    {
        private readonly IReadOnlyList<NormalizedSeerrRequest> _requests;
        private readonly Exception? _exception;

        public FakeSeerrClient(IReadOnlyList<NormalizedSeerrRequest> requests, Exception? exception)
        {
            _requests = requests;
            _exception = exception;
        }

        public Task<SeerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new SeerrConnectionTestResult { IsSuccess = true });

        public Task<IReadOnlyList<NormalizedSeerrRequest>> GetRequestsAsync(CancellationToken cancellationToken)
            => _exception is null ? Task.FromResult(_requests) : Task.FromException<IReadOnlyList<NormalizedSeerrRequest>>(_exception);
    }

    private sealed class FakeMediaMatcher : IJellyfinMediaMatcher
    {
        private readonly Dictionary<long, MediaMatchResult> _matches;

        public FakeMediaMatcher(Dictionary<long, MediaMatchResult> matches)
        {
            _matches = matches;
        }

        public Task<MediaMatchResult> MatchRequestAsync(string jellyfinUserId, NormalizedSeerrRequest request, CancellationToken cancellationToken)
            => Task.FromResult(_matches.TryGetValue(request.RequestId, out var result)
                ? result
                : new MediaMatchResult { IsMatch = false, FailureReason = "No match" });
    }

    private sealed class FakeWatchlistAdapter : IKefinTweaksWatchlistAdapter
    {
        private readonly HashSet<string> _watchlistItemIds;

        public FakeWatchlistAdapter()
            : this([])
        {
        }

        public FakeWatchlistAdapter(HashSet<string> watchlistItemIds)
        {
            _watchlistItemIds = watchlistItemIds;
        }

        public List<SyncCandidate> AddCalls { get; } = [];

        public Task<CompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CompatibilityResult { IsCompatible = true, Message = "ok" });

        public Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(_watchlistItemIds, StringComparer.OrdinalIgnoreCase));

        public Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken)
        {
            AddCalls.Add(candidate);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTagSyncService : ITagSyncService
    {
        private readonly Dictionary<string, IReadOnlyList<SyncCandidate>> _itemsByUser;

        public FakeTagSyncService(Dictionary<string, IReadOnlyList<SyncCandidate>> itemsByUser)
        {
            _itemsByUser = itemsByUser;
        }

        public Task<IReadOnlyList<SyncCandidate>> GetTagCandidatesAsync(UserSyncSettings userSettings, CancellationToken cancellationToken)
            => Task.FromResult(_itemsByUser.TryGetValue(userSettings.JellyfinUserId, out var items)
                ? items
                : Array.Empty<SyncCandidate>());
    }

    private sealed class FakeJellyfinApi : IJellyfinApi
    {
        public Dictionary<(string UserId, RequestMediaType MediaType, string ProviderName, string ProviderValue), IReadOnlyList<JellyfinLibraryItem>> ProviderSearchResults { get; } = [];

        public Dictionary<(string UserId, RequestMediaType MediaType, string Title, int? Year), IReadOnlyList<JellyfinLibraryItem>> TitleSearchResults { get; } = [];

        public Task<IReadOnlyList<JellyfinUserInfo>> GetUsersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JellyfinUserInfo>>(Array.Empty<JellyfinUserInfo>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> GetWatchlistItemsAsync(string jellyfinUserId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(Array.Empty<JellyfinLibraryItem>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByProviderIdAsync(string jellyfinUserId, RequestMediaType mediaType, string providerName, string providerValue, CancellationToken cancellationToken)
            => Task.FromResult(ProviderSearchResults.TryGetValue((jellyfinUserId, mediaType, providerName, providerValue), out var items) ? items : Array.Empty<JellyfinLibraryItem>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByTitleYearAsync(string jellyfinUserId, RequestMediaType mediaType, string title, int? year, CancellationToken cancellationToken)
            => Task.FromResult(TitleSearchResults.TryGetValue((jellyfinUserId, mediaType, title, year), out var items) ? items : Array.Empty<JellyfinLibraryItem>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> GetItemsByTagAsync(string jellyfinUserId, string tag, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(Array.Empty<JellyfinLibraryItem>());

        public Task SetItemLikeAsync(string jellyfinUserId, string jellyfinItemId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsKefinTweaksInstalledAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
