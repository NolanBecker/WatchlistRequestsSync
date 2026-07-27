namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public enum MediaKind
{
    Movie,
    Series
}

public enum ArrSourceKind
{
    Sonarr,
    Radarr
}

public sealed class ProviderIdSet
{
    public string Tmdb { get; set; } = string.Empty;

    public string Tvdb { get; set; } = string.Empty;

    public string Imdb { get; set; } = string.Empty;
}

public sealed class ArrMediaItem
{
    public ArrSourceKind Source { get; set; }

    public int SourceItemId { get; set; }

    public MediaKind MediaKind { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public ProviderIdSet ProviderIds { get; set; } = new();

    public IReadOnlyList<int> TagIds { get; set; } = Array.Empty<int>();

    public IReadOnlyList<string> TagLabels { get; set; } = Array.Empty<string>();
}

public sealed class ArrConnectionTestResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string NormalizedBaseUrl { get; set; } = string.Empty;
}

public sealed class SourceConnectionResult
{
    public ArrSourceKind Source { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string NormalizedBaseUrl { get; set; } = string.Empty;
}

public sealed class ConnectionTestResult
{
    public bool IsSuccess { get; set; }

    public List<SourceConnectionResult> Sources { get; set; } = [];
}

public sealed class ConnectionTestRequest
{
    public string SonarrBaseUrl { get; set; } = string.Empty;

    public string SonarrApiKey { get; set; } = string.Empty;

    public string RadarrBaseUrl { get; set; } = string.Empty;

    public string RadarrApiKey { get; set; } = string.Empty;
}
