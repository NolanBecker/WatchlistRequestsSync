using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class UserMappingService : IUserMappingService
{
    public string? MapSeerrRequestToJellyfinUser(NormalizedSeerrRequest request, IReadOnlyDictionary<long, string> explicitMappings)
    {
        if (!string.IsNullOrWhiteSpace(request.JellyfinUserId))
        {
            return request.JellyfinUserId;
        }

        return explicitMappings.TryGetValue(request.SeerrUserId, out var jellyfinUserId)
            ? jellyfinUserId
            : null;
    }
}
