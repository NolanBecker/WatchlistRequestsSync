using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.WatchlistRequestsSync.ScheduledTasks;

public sealed class WatchlistRequestsSyncTask : IScheduledTask
{
    private readonly ISyncService _syncService;
    private readonly IPluginConfigurationAccessor _configurationAccessor;

    public WatchlistRequestsSyncTask(ISyncService syncService, IPluginConfigurationAccessor configurationAccessor)
    {
        _syncService = syncService;
        _configurationAccessor = configurationAccessor;
    }

    public string Name => "Watchlist Requests Sync";

    public string Description => "Synchronizes Seerr or Jellyseerr requests into user watchlists using additive Likes updates.";

    public string Category => "Plugins";

    public string Key => "WatchlistRequestsSync";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);
        await _syncService.RunAsync(SyncRunMode.Scheduled, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var minutes = Math.Max(_configurationAccessor.GetConfiguration().SyncIntervalMinutes, 15);
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromMinutes(minutes).Ticks
            }
        ];
    }
}
