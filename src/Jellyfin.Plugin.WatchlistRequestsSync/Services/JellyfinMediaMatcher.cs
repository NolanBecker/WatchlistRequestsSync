using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class JellyfinMediaMatcher : IJellyfinMediaMatcher
{
    private readonly IJellyfinApi _jellyfinApi;

    public JellyfinMediaMatcher(IJellyfinApi jellyfinApi)
    {
        _jellyfinApi = jellyfinApi;
    }

    public async Task<MediaMatchResult> MatchItemAsync(string jellyfinUserId, ArrMediaItem item, CancellationToken cancellationToken)
    {
        var providerMatches = await FindProviderMatchesAsync(jellyfinUserId, item, cancellationToken).ConfigureAwait(false);
        if (providerMatches.Count == 1)
        {
            return ToMatch(providerMatches[0]);
        }

        if (providerMatches.Count > 1)
        {
            return new MediaMatchResult
            {
                IsMatch = false,
                IsAmbiguous = true,
                FailureReason = "Multiple provider-ID matches were found."
            };
        }

        var fallbackMatches = await _jellyfinApi.FindItemsByTitleYearAsync(jellyfinUserId, item.MediaKind, item.Title, item.Year, cancellationToken).ConfigureAwait(false);
        if (fallbackMatches.Count == 1)
        {
            return ToMatch(fallbackMatches[0]);
        }

        if (fallbackMatches.Count > 1)
        {
            return new MediaMatchResult
            {
                IsMatch = false,
                IsAmbiguous = true,
                FailureReason = "Exact title/year matching returned multiple items."
            };
        }

        return new MediaMatchResult
        {
            IsMatch = false,
            FailureReason = "No Jellyfin library match was found."
        };
    }

    private async Task<List<JellyfinLibraryItem>> FindProviderMatchesAsync(string jellyfinUserId, ArrMediaItem item, CancellationToken cancellationToken)
    {
        var keys = item.MediaKind == MediaKind.Movie
            ? new[] { ("Tmdb", item.ProviderIds.Tmdb), ("Imdb", item.ProviderIds.Imdb) }
            : new[]
            {
                ("Tvdb", item.ProviderIds.Tvdb),
                ("Tmdb", item.ProviderIds.Tmdb),
                ("Imdb", item.ProviderIds.Imdb)
            };

        foreach (var (providerName, providerValue) in keys)
        {
            if (string.IsNullOrWhiteSpace(providerValue))
            {
                continue;
            }

            var matches = await _jellyfinApi.FindItemsByProviderIdAsync(jellyfinUserId, item.MediaKind, providerName, providerValue, cancellationToken).ConfigureAwait(false);
            if (matches.Count > 0)
            {
                return matches.ToList();
            }
        }

        return [];
    }

    private static MediaMatchResult ToMatch(JellyfinLibraryItem item)
        => new()
        {
            IsMatch = true,
            JellyfinItemId = item.Id,
            ItemName = item.Name
        };
}
