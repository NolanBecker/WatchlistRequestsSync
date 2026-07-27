using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;
using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using Xunit;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Tests;

public sealed class SyncServiceTests
{
    [Fact]
    public async Task DuplicatePrevention_SkipsAlreadyLikedItems()
    {
        var service = CreateSyncService(
            sourceItems: [CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception")],
            mediaMatchResults: new Dictionary<string, MediaMatchResult> { ["Radarr:101"] = Match("item-1", "Inception") },
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
            sourceItems: [CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception")],
            mediaMatchResults: new Dictionary<string, MediaMatchResult> { ["Radarr:101"] = Match("item-1", "Inception") },
            adapter: fakeAdapter,
            configurationOverride: config => config.DryRun = true);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.True(result.IsDryRun);
        Assert.Empty(fakeAdapter.AddCalls);
        Assert.Single(result.Users[0].AddedItems);
    }

    [Fact]
    public async Task UserSettings_FilterItemsByMediaType()
    {
        var service = CreateSyncService(
            sourceItems:
            [
                CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception"),
                CreateItem(ArrSourceKind.Sonarr, 202, MediaKind.Series, "Dark")
            ],
            mediaMatchResults: new Dictionary<string, MediaMatchResult>
            {
                ["Radarr:101"] = Match("movie-1", "Inception"),
                ["Sonarr:202"] = Match("series-1", "Dark")
            },
            configurationOverride: config => config.Users[0].IncludeMovies = false);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.Single(result.Users[0].AddedItems);
        Assert.Equal("series-1", result.Users[0].AddedItems[0].JellyfinItemId);
    }

    [Fact]
    public async Task SourceFailure_IsReportedWithoutWrites()
    {
        var fakeAdapter = new FakeWatchlistAdapter();
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>(),
            adapter: fakeAdapter,
            radarrException: new HttpRequestException("boom"));

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Empty(fakeAdapter.AddCalls);
    }

    [Fact]
    public async Task Preview_UsesProvidedConfigurationWhenPersistedConfigIsStale()
    {
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>(),
            configurationOverride: config => config.RadarrBaseUrl = string.Empty,
            radarrItemsForProvidedConfig: [CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception")],
            overrideMediaMatchResults: new Dictionary<string, MediaMatchResult> { ["Radarr:101"] = Match("item-1", "Inception") });

        var result = await service.PreviewAsync(new PluginConfiguration
        {
            IsEnabled = true,
            RadarrBaseUrl = "http://radarr.local",
            RadarrApiKey = "key",
            RadarrTags = "watchlist",
            Users =
            [
                new UserSyncSettings
                {
                    JellyfinUserId = "user-a",
                    JellyfinUserName = "User A",
                    IsEnabled = true
                }
            ]
        }, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Single(result.Users[0].AddedItems);
    }

    [Fact]
    public async Task ManualRun_UsesProvidedConfigurationWhenPersistedConfigIsStale()
    {
        var fakeAdapter = new FakeWatchlistAdapter();
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>(),
            adapter: fakeAdapter,
            configurationOverride: config => config.RadarrBaseUrl = string.Empty,
            radarrItemsForProvidedConfig: [CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception")],
            overrideMediaMatchResults: new Dictionary<string, MediaMatchResult> { ["Radarr:101"] = Match("item-1", "Inception") });

        var result = await service.RunAsync(SyncRunMode.Manual, new PluginConfiguration
        {
            IsEnabled = true,
            RadarrBaseUrl = "http://radarr.local",
            RadarrApiKey = "key",
            RadarrTags = "watchlist",
            Users =
            [
                new UserSyncSettings
                {
                    JellyfinUserId = "user-a",
                    JellyfinUserName = "User A",
                    IsEnabled = true
                }
            ]
        }, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Single(fakeAdapter.AddCalls);
    }

    [Fact]
    public async Task WarningCompatibility_DoesNotBlockSync()
    {
        var adapter = new FakeWatchlistAdapter
        {
            Compatibility = new CompatibilityResult
            {
                IsCompatible = true,
                Severity = CompatibilitySeverity.Warning,
                Message = "warning"
            }
        };

        var service = CreateSyncService(
            sourceItems: [CreateItem(ArrSourceKind.Radarr, 101, MediaKind.Movie, "Inception")],
            mediaMatchResults: new Dictionary<string, MediaMatchResult> { ["Radarr:101"] = Match("item-1", "Inception") },
            adapter: adapter);

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.True(result.CompatibilityOk);
        Assert.Equal("warning", result.CompatibilityMessage);
        Assert.Single(result.Users[0].AddedItems);
    }

    [Fact]
    public async Task ProviderIdMatcher_PrefersProviderMatch()
    {
        var api = new FakeJellyfinApi
        {
            ProviderSearchResults =
            {
                [("user-a", MediaKind.Movie, "Tmdb", "27205")] =
                    [new JellyfinLibraryItem { Id = "movie-1", Name = "Inception", Type = "Movie" }]
            }
        };
        var matcher = new JellyfinMediaMatcher(api);

        var result = await matcher.MatchItemAsync("user-a", new ArrMediaItem
        {
            Source = ArrSourceKind.Radarr,
            SourceItemId = 1,
            MediaKind = MediaKind.Movie,
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
                [("user-a", MediaKind.Series, "Dark", 2017)] =
                [
                    new JellyfinLibraryItem { Id = "series-1", Name = "Dark", Type = "Series", ProductionYear = 2017 },
                    new JellyfinLibraryItem { Id = "series-2", Name = "Dark", Type = "Series", ProductionYear = 2017 }
                ]
            }
        };
        var matcher = new JellyfinMediaMatcher(api);

        var result = await matcher.MatchItemAsync("user-a", new ArrMediaItem
        {
            Source = ArrSourceKind.Sonarr,
            SourceItemId = 2,
            MediaKind = MediaKind.Series,
            Title = "Dark",
            Year = 2017
        }, CancellationToken.None);

        Assert.False(result.IsMatch);
        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public async Task TestConnection_CombinesConfiguredSources()
    {
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>(),
            sonarrConnectionSuccess: true,
            radarrConnectionSuccess: false);

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            SonarrBaseUrl = "http://sonarr.local",
            SonarrApiKey = "sonarr-key",
            RadarrBaseUrl = "http://radarr.local",
            RadarrApiKey = "radarr-key"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Sources.Count);
        Assert.Contains(result.Sources, source => source.Source == ArrSourceKind.Sonarr && source.IsSuccess);
        Assert.Contains(result.Sources, source => source.Source == ArrSourceKind.Radarr && !source.IsSuccess);
    }

    [Fact]
    public async Task TestConnection_FailsWhenNoSourcesConfigured()
    {
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>());

        var result = await service.TestConnectionAsync(new ConnectionTestRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.All(result.Sources, source => Assert.False(source.IsEnabled));
    }

    [Fact]
    public async Task RunReportsWhenNoTaggedItemsAreFound()
    {
        var service = CreateSyncService(
            sourceItems: Array.Empty<ArrMediaItem>(),
            mediaMatchResults: new Dictionary<string, MediaMatchResult>());

        var result = await service.RunAsync(SyncRunMode.Manual, CancellationToken.None);

        Assert.Contains(result.Errors, error => error.Contains("No tagged Sonarr or Radarr items were found.", StringComparison.Ordinal));
    }

    private static SyncService CreateSyncService(
        IReadOnlyList<ArrMediaItem> sourceItems,
        Dictionary<string, MediaMatchResult> mediaMatchResults,
        HashSet<string>? watchlistItemIds = null,
        FakeWatchlistAdapter? adapter = null,
        Exception? sonarrException = null,
        Exception? radarrException = null,
        Action<PluginConfiguration>? configurationOverride = null,
        IReadOnlyList<ArrMediaItem>? radarrItemsForProvidedConfig = null,
        Dictionary<string, MediaMatchResult>? overrideMediaMatchResults = null,
        bool sonarrConnectionSuccess = true,
        bool radarrConnectionSuccess = true)
    {
        var configuration = new PluginConfiguration
        {
            IsEnabled = true,
            SonarrBaseUrl = "http://sonarr.local",
            SonarrApiKey = "sonarr-key",
            SonarrTags = "watchlist-shows",
            RadarrBaseUrl = "http://radarr.local",
            RadarrApiKey = "radarr-key",
            RadarrTags = "watchlist-movies",
            Users =
            [
                new UserSyncSettings
                {
                    JellyfinUserId = "user-a",
                    JellyfinUserName = "User A",
                    IsEnabled = true
                }
            ]
        };
        configurationOverride?.Invoke(configuration);

        var providedConfigItems = radarrItemsForProvidedConfig ?? Array.Empty<ArrMediaItem>();
        var matcherResults = overrideMediaMatchResults ?? mediaMatchResults;

        return new SyncService(
            new FakeConfigurationAccessor(configuration),
            new FakePluginStateStore(),
            [
                new FakeArrClient(ArrSourceKind.Sonarr, sourceItems.Where(item => item.Source == ArrSourceKind.Sonarr).ToList(), sonarrException, sonarrConnectionSuccess),
                new FakeArrClient(
                    ArrSourceKind.Radarr,
                    sourceItems.Where(item => item.Source == ArrSourceKind.Radarr).ToList(),
                    radarrException,
                    radarrConnectionSuccess,
                    new Dictionary<string, IReadOnlyList<ArrMediaItem>>
                    {
                        ["http://radarr.local|watchlist"] = providedConfigItems
                    })
            ],
            new FakeMediaMatcher(matcherResults),
            adapter ?? new FakeWatchlistAdapter(watchlistItemIds ?? []));
    }

    private static ArrMediaItem CreateItem(ArrSourceKind source, int sourceItemId, MediaKind mediaKind, string title)
        => new()
        {
            Source = source,
            SourceItemId = sourceItemId,
            MediaKind = mediaKind,
            Title = title,
            Year = mediaKind == MediaKind.Movie ? 2010 : 2017,
            ProviderIds = mediaKind == MediaKind.Movie
                ? new ProviderIdSet { Tmdb = "27205" }
                : new ProviderIdSet { Tvdb = "318408" }
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

    private sealed class FakeArrClient : IArrClient
    {
        private readonly IReadOnlyList<ArrMediaItem> _items;
        private readonly Exception? _exception;
        private readonly Dictionary<string, IReadOnlyList<ArrMediaItem>> _overrides;
        private readonly bool _connectionSuccess;

        public FakeArrClient(
            ArrSourceKind source,
            IReadOnlyList<ArrMediaItem> items,
            Exception? exception,
            bool connectionSuccess,
            Dictionary<string, IReadOnlyList<ArrMediaItem>>? overrides = null)
        {
            Source = source;
            _items = items;
            _exception = exception;
            _connectionSuccess = connectionSuccess;
            _overrides = overrides ?? [];
        }

        public ArrSourceKind Source { get; }

        public Task<ArrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ArrConnectionTestResult { IsSuccess = _connectionSuccess });

        public Task<ArrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
            => Task.FromResult(new ArrConnectionTestResult
            {
                IsSuccess = _connectionSuccess,
                Message = _connectionSuccess ? "Connection succeeded." : "Connection failed.",
                NormalizedBaseUrl = baseUrl
            });

        public Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(CancellationToken cancellationToken)
            => _exception is null
                ? Task.FromResult(_items)
                : Task.FromException<IReadOnlyList<ArrMediaItem>>(_exception);

        public Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(string baseUrl, string apiKey, string configuredTags, CancellationToken cancellationToken)
        {
            if (_exception is not null)
            {
                return Task.FromException<IReadOnlyList<ArrMediaItem>>(_exception);
            }

            var key = $"{baseUrl}|{configuredTags}";
            if (_overrides.TryGetValue(key, out var overrideItems))
            {
                return Task.FromResult(overrideItems);
            }

            return Task.FromResult(_items);
        }
    }

    private sealed class FakeMediaMatcher : IJellyfinMediaMatcher
    {
        private readonly Dictionary<string, MediaMatchResult> _matches;

        public FakeMediaMatcher(Dictionary<string, MediaMatchResult> matches)
        {
            _matches = matches;
        }

        public Task<MediaMatchResult> MatchItemAsync(string jellyfinUserId, ArrMediaItem item, CancellationToken cancellationToken)
            => Task.FromResult(_matches.TryGetValue($"{item.Source}:{item.SourceItemId}", out var result)
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

        public CompatibilityResult Compatibility { get; set; } = new() { IsCompatible = true, Severity = CompatibilitySeverity.Ok, Message = "ok" };

        public Task<CompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(Compatibility);

        public Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(_watchlistItemIds, StringComparer.OrdinalIgnoreCase));

        public Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken)
        {
            AddCalls.Add(candidate);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJellyfinApi : IJellyfinApi
    {
        public Dictionary<(string UserId, MediaKind MediaKind, string ProviderName, string ProviderValue), IReadOnlyList<JellyfinLibraryItem>> ProviderSearchResults { get; } = [];

        public Dictionary<(string UserId, MediaKind MediaKind, string Title, int? Year), IReadOnlyList<JellyfinLibraryItem>> TitleSearchResults { get; } = [];

        public Task<IReadOnlyList<JellyfinUserInfo>> GetUsersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<JellyfinUserInfo>>(Array.Empty<JellyfinUserInfo>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> GetWatchlistItemsAsync(string jellyfinUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(Array.Empty<JellyfinLibraryItem>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByProviderIdAsync(string jellyfinUserId, MediaKind mediaKind, string providerName, string providerValue, CancellationToken cancellationToken)
            => Task.FromResult(ProviderSearchResults.TryGetValue((jellyfinUserId, mediaKind, providerName, providerValue), out var items) ? items : Array.Empty<JellyfinLibraryItem>());

        public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByTitleYearAsync(string jellyfinUserId, MediaKind mediaKind, string title, int? year, CancellationToken cancellationToken)
            => Task.FromResult(TitleSearchResults.TryGetValue((jellyfinUserId, mediaKind, title, year), out var items) ? items : Array.Empty<JellyfinLibraryItem>());

        public Task SetItemLikeAsync(string jellyfinUserId, string jellyfinItemId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<CompatibilityResult> GetKefinTweaksCompatibilityAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CompatibilityResult
            {
                IsCompatible = true,
                Severity = CompatibilitySeverity.Ok,
                Message = "detected"
            });
    }
}
