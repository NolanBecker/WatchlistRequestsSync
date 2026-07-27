using Jellyfin.Data.Enums;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class JellyfinApi : IJellyfinApi
{
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IPluginManager _pluginManager;

    public JellyfinApi(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IPluginManager pluginManager)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _pluginManager = pluginManager;
    }

    public Task<IReadOnlyList<JellyfinUserInfo>> GetUsersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<JellyfinUserInfo> users = _userManager.Users
            .Select(static user => new JellyfinUserInfo
            {
                Id = user.Id.ToString("N"),
                Name = user.Username
            })
            .ToList();
        return Task.FromResult(users);
    }

    public Task<IReadOnlyList<JellyfinLibraryItem>> GetWatchlistItemsAsync(string jellyfinUserId, CancellationToken cancellationToken)
    {
        var user = GetUser(jellyfinUserId);
        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            Recursive = true,
            IsLiked = true
        });
        return Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(items.Select(ToLibraryItem).ToList());
    }

    public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByProviderIdAsync(string jellyfinUserId, MediaKind mediaKind, string providerName, string providerValue, CancellationToken cancellationToken)
    {
        var user = GetUser(jellyfinUserId);
        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            Recursive = true,
            IncludeItemTypes = GetItemTypes(mediaKind),
            HasAnyProviderId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [providerName] = providerValue
            }
        });

        return Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(items.Select(ToLibraryItem).ToList());
    }

    public Task<IReadOnlyList<JellyfinLibraryItem>> FindItemsByTitleYearAsync(string jellyfinUserId, MediaKind mediaKind, string title, int? year, CancellationToken cancellationToken)
    {
        var user = GetUser(jellyfinUserId);
        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            Recursive = true,
            SearchTerm = title,
            IncludeItemTypes = GetItemTypes(mediaKind),
            Years = year.HasValue ? [year.Value] : []
        });

        var exact = items
            .Where(item => string.Equals(item.Name, title, StringComparison.OrdinalIgnoreCase))
            .Where(item => !year.HasValue || item.ProductionYear == year.Value)
            .Select(ToLibraryItem)
            .ToList();
        return Task.FromResult<IReadOnlyList<JellyfinLibraryItem>>(exact);
    }

    public Task SetItemLikeAsync(string jellyfinUserId, string jellyfinItemId, CancellationToken cancellationToken)
    {
        var user = GetUser(jellyfinUserId);
        var item = _libraryManager.GetItemById(Guid.Parse(jellyfinItemId))
            ?? throw new InvalidOperationException($"Jellyfin item {jellyfinItemId} was not found.");
        _userDataManager.SaveUserData(
            user,
            item,
            new UpdateUserItemDataDto
            {
                Likes = true
            },
            UserDataSaveReason.UpdateUserRating);
        return Task.CompletedTask;
    }

    public Task<CompatibilityResult> GetKefinTweaksCompatibilityAsync(CancellationToken cancellationToken)
    {
        var plugins = (_pluginManager.Plugins ?? []).ToList();

        static bool Matches(object plugin)
        {
            var type = plugin.GetType();
            var textCandidates = new[]
            {
                type.GetProperty("Name")?.GetValue(plugin)?.ToString(),
                type.GetProperty("Description")?.GetValue(plugin)?.ToString(),
                type.GetProperty("AssemblyFileName")?.GetValue(plugin)?.ToString(),
                type.GetProperty("DataFolderPath")?.GetValue(plugin)?.ToString(),
                type.FullName,
                type.Assembly.GetName().Name
            };

            return textCandidates.Any(static value =>
                !string.IsNullOrWhiteSpace(value)
                && value.Contains("kefin", StringComparison.OrdinalIgnoreCase));
        }

        var matchedPlugin = plugins.FirstOrDefault(Matches);
        if (matchedPlugin is not null)
        {
            var matchedName = matchedPlugin.GetType().GetProperty("Name")?.GetValue(matchedPlugin)?.ToString()
                ?? matchedPlugin.GetType().Assembly.GetName().Name
                ?? "KefinTweaks-compatible plugin";

            return Task.FromResult(new CompatibilityResult
            {
                IsCompatible = true,
                Severity = CompatibilitySeverity.Ok,
                Message = $"KefinTweaks detected via plugin metadata: {matchedName}. Watchlist writes will use Jellyfin Likes as documented by KefinTweaks."
            });
        }

        // KefinTweaks is a front-end plugin, and its Watchlist is documented to use Jellyfin Likes.
        // Positive server-side plugin detection is helpful but not required for additive Likes-based sync.
        return Task.FromResult(new CompatibilityResult
        {
            IsCompatible = true,
            Severity = CompatibilitySeverity.Warning,
            Message = "KefinTweaks was not positively detected from loaded plugin metadata, but sync will proceed because the Watchlist integration uses Jellyfin Likes."
        });
    }

    private User GetUser(string jellyfinUserId)
        => _userManager.GetUserById(Guid.Parse(jellyfinUserId))
           ?? throw new InvalidOperationException($"Jellyfin user {jellyfinUserId} was not found.");

    private static JellyfinLibraryItem ToLibraryItem(BaseItem item)
        => new()
        {
            Id = item.Id.ToString("N"),
            Name = item.Name,
            ProductionYear = item.ProductionYear,
            Type = item switch
            {
                Series => "Series",
                _ => item.GetType().Name
            },
            ProviderIds = new ProviderIdSet
            {
                Tmdb = item.GetProviderId(MetadataProvider.Tmdb) ?? string.Empty,
                Tvdb = item.GetProviderId(MetadataProvider.Tvdb) ?? string.Empty,
                Imdb = item.GetProviderId(MetadataProvider.Imdb) ?? string.Empty
            },
            Tags = item.Tags?.ToArray() ?? Array.Empty<string>()
        };

    private static BaseItemKind[] GetItemTypes(MediaKind mediaKind)
        => mediaKind == MediaKind.Movie ? [BaseItemKind.Movie] : [BaseItemKind.Series];
}
