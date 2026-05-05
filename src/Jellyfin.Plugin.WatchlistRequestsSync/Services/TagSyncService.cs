using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class TagSyncService : ITagSyncService
{
    private readonly IJellyfinApi _jellyfinApi;

    public TagSyncService(IJellyfinApi jellyfinApi)
    {
        _jellyfinApi = jellyfinApi;
    }

    public async Task<IReadOnlyList<SyncCandidate>> GetTagCandidatesAsync(UserSyncSettings userSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userSettings.MediaTag))
        {
            return Array.Empty<SyncCandidate>();
        }

        var items = await _jellyfinApi.GetItemsByTagAsync(userSettings.JellyfinUserId, userSettings.MediaTag, cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => IncludeType(userSettings, item.Type))
            .Select(item => new SyncCandidate
            {
                JellyfinUserId = userSettings.JellyfinUserId,
                JellyfinItemId = item.Id,
                ItemName = item.Name,
                Source = SyncItemSource.Tag,
                ProviderIds = item.ProviderIds
            })
            .ToList();
    }

    private static bool IncludeType(UserSyncSettings settings, string jellyfinType)
        => (settings.IncludeMovies && string.Equals(jellyfinType, "Movie", StringComparison.OrdinalIgnoreCase))
           || (settings.IncludeSeries && string.Equals(jellyfinType, "Series", StringComparison.OrdinalIgnoreCase));
}
