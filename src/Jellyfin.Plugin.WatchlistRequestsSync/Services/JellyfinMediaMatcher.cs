using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class JellyfinMediaMatcher : IJellyfinMediaMatcher
{
    private readonly IJellyfinApi _jellyfinApi;

    public JellyfinMediaMatcher(IJellyfinApi jellyfinApi)
    {
        _jellyfinApi = jellyfinApi;
    }

    public async Task<MediaMatchResult> MatchRequestAsync(string jellyfinUserId, NormalizedSeerrRequest request, CancellationToken cancellationToken)
    {
        var providerMatches = await FindProviderMatchesAsync(jellyfinUserId, request, cancellationToken).ConfigureAwait(false);
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

        var fallbackMatches = await _jellyfinApi.FindItemsByTitleYearAsync(jellyfinUserId, request.MediaType, request.Title, request.Year, cancellationToken).ConfigureAwait(false);
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

    private async Task<List<JellyfinLibraryItem>> FindProviderMatchesAsync(string jellyfinUserId, NormalizedSeerrRequest request, CancellationToken cancellationToken)
    {
        var keys = request.MediaType == RequestMediaType.Movie
            ? new[] { ("Tmdb", request.ProviderIds.Tmdb) }
            : new[]
            {
                ("Tvdb", request.ProviderIds.Tvdb),
                ("Tmdb", request.ProviderIds.Tmdb),
                ("Imdb", request.ProviderIds.Imdb)
            };

        foreach (var (providerName, providerValue) in keys)
        {
            if (string.IsNullOrWhiteSpace(providerValue))
            {
                continue;
            }

            var matches = await _jellyfinApi.FindItemsByProviderIdAsync(jellyfinUserId, request.MediaType, providerName, providerValue, cancellationToken).ConfigureAwait(false);
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
