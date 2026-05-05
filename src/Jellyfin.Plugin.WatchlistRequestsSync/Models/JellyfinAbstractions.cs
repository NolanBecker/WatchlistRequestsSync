namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public sealed class JellyfinUserInfo
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class JellyfinLibraryItem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? ProductionYear { get; set; }

    public string Type { get; set; } = string.Empty;

    public ProviderIdSet ProviderIds { get; set; } = new();

    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

public sealed class CompatibilityResult
{
    public bool IsCompatible { get; set; }

    public string Message { get; set; } = string.Empty;
}
