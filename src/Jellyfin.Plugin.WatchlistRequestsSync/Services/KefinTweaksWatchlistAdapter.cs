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
    {
        var installed = await _jellyfinApi.IsKefinTweaksInstalledAsync(cancellationToken).ConfigureAwait(false);
        return new CompatibilityResult
        {
            IsCompatible = installed,
            Message = installed
                ? "KefinTweaks was detected. Watchlist writes will use Jellyfin Likes as documented by KefinTweaks."
                : "KefinTweaks was not detected. No watchlist writes will be attempted."
        };
    }

    public async Task<IReadOnlySet<string>> GetWatchlistItemIdsAsync(string jellyfinUserId, CancellationToken cancellationToken)
    {
        var items = await _jellyfinApi.GetWatchlistItemsAsync(jellyfinUserId, cancellationToken).ConfigureAwait(false);
        return new HashSet<string>(items.Select(static item => item.Id), StringComparer.OrdinalIgnoreCase);
    }

    public Task AddToWatchlistAsync(string jellyfinUserId, SyncCandidate candidate, CancellationToken cancellationToken)
        // KefinTweaks documents its watchlist as Jellyfin Likes-driven. We only ever set Likes=true.
        => _jellyfinApi.SetItemLikeAsync(jellyfinUserId, candidate.JellyfinItemId, cancellationToken);
}
