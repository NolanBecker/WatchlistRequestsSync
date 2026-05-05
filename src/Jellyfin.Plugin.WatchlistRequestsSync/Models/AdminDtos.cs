using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Models;

public sealed class PluginStatusDto
{
    public bool IsEnabled { get; set; }

    public string LastSyncUtc { get; set; } = string.Empty;

    public bool CompatibilityOk { get; set; }

    public string CompatibilityMessage { get; set; } = string.Empty;
}

public sealed class UserSettingsDto
{
    public string JellyfinUserId { get; set; } = string.Empty;

    public string JellyfinUserName { get; set; } = string.Empty;

    public UserSyncSettings Settings { get; set; } = new();
}
