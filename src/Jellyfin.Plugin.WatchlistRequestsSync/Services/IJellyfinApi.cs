using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public interface IJellyfinApi
{
    Task<IReadOnlyList<JellyfinUserInfo>> GetUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> GetWatchlistItemsAsync(string jellyfinUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByProviderIdAsync(string jellyfinUserId, MediaKind mediaKind, string providerName, string providerValue, CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByTitleYearAsync(string jellyfinUserId, MediaKind mediaKind, string title, int? year, CancellationToken cancellationToken);

    Task SetItemLikeAsync(string jellyfinUserId, string jellyfinItemId, CancellationToken cancellationToken);

    Task<CompatibilityResult> GetKefinTweaksCompatibilityAsync(CancellationToken cancellationToken);
}
