namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public enum RequestMediaType
{
    Movie,
    Series
}

public enum RequestApprovalState
{
    Unknown,
    Pending,
    Approved
}

public enum RequestAvailabilityState
{
    Unknown,
    Pending,
    Processing,
    PartiallyAvailable,
    Available
}

public sealed class ProviderIdSet
{
    public string Tmdb { get; set; } = string.Empty;

    public string Tvdb { get; set; } = string.Empty;

    public string Imdb { get; set; } = string.Empty;
}

public sealed class NormalizedSeerrRequest
{
    public long RequestId { get; set; }

    public long SeerrUserId { get; set; }

    public string JellyfinUserId { get; set; } = string.Empty;

    public string RequestingUserName { get; set; } = string.Empty;

    public RequestMediaType MediaType { get; set; }

    public RequestApprovalState ApprovalState { get; set; }

    public RequestAvailabilityState AvailabilityState { get; set; }

    public ProviderIdSet ProviderIds { get; set; } = new();

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public bool IsSeasonSpecificRequest { get; set; }
}

public sealed class SeerrConnectionTestResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string NormalizedBaseUrl { get; set; } = string.Empty;
}
