using System.Reflection;
using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WatchlistRequestsSync;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Watchlist Requests Sync";

    public override Guid Id => Guid.Parse("7b2cef2e-d5ea-43e2-be8e-ab2070b2d18e");

    public override string Description => "Syncs Seerr or Jellyseerr requests into each Jellyfin user's KefinTweaks watchlist using additive Likes updates.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var assembly = GetType().GetTypeInfo().Assembly;

        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{assembly.GetName().Name}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = $"{Name}.js",
                EmbeddedResourcePath = $"{assembly.GetName().Name}.Configuration.configPage.js"
            },
            new PluginPageInfo
            {
                Name = $"{Name}.css",
                EmbeddedResourcePath = $"{assembly.GetName().Name}.Configuration.configPage.css"
            }
        ];
    }
}
