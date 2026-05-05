using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public interface IJellyfinApi
{
    Task<IReadOnlyList<JellyfinUserInfo>> GetUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> GetWatchlistItemsAsync(string jellyfinUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByProviderIdAsync(string jellyfinUserId, RequestMediaType mediaType, string providerName, string providerValue, CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByTitleYearAsync(string jellyfinUserId, RequestMediaType mediaType, string title, int? year, CancellationToken cancellationToken);

    Task<IReadOnlyList<JellyfinLibraryItem>> GetItemsByTagAsync(string jellyfinUserId, string tag, CancellationToken cancellationToken);

    Task SetItemLikeAsync(string jellyfinUserId, string jellyfinItemId, CancellationToken cancellationToken);

    Task<bool> IsKefinTweaksInstalledAsync(CancellationToken cancellationToken);
}
