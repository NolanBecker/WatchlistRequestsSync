using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class KefinTweaksWatchlistAdapter : IKefinTweaksWatchlistAdapter
{
    private readonly IJellyfinApi _jellyfinApi;

    public KefinTweaksWatchlistAdapter(IJellyfinApi jellyfinApi)
    {
        _jellyfinApi = jellyfinApi;
    }

    public async Task<CompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken)
        => await _jellyfinApi.GetKefinTweaksCompatibilityAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken)
    {
        var items = await _jellyfinApi.GetWatchlistItemsAsync(jellyfinUserId, cancellationToken).ConfigureAwait(false);
        return new HashSet<string>(items.Select(static item => item.Id), StringComparer.OrdinalIgnoreCase);
    }

    public Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken)
        // KefinTweaks documents its watchlist as Jellyfin Likes-driven. We only ever set Likes=true.
        => _jellyfinApi.SetItemLikeAsync(jellyfinUserId, candidate.JellyfinItemId, cancellationToken);
}
