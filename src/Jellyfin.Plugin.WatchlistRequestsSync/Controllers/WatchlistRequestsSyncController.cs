using Jellyfin.Plugin.WatchlistRequestsSync.Models;
using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Controllers;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("Plugins/WatchlistRequestsSync")]
public sealed class WatchlistRequestsSyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly IPluginConfigurationAccessor _configurationAccessor;
    private readonly IPluginStateStore _stateStore;
    private readonly IKefinTweaksWatchlistAdapter _watchlistAdapter;
    private readonly IJellyfinApi _jellyfinApi;

    public WatchlistRequestsSyncController(
        ISyncService syncService,
        IPluginConfigurationAccessor configurationAccessor,
        IPluginStateStore stateStore,
        IKefinTweaksWatchlistAdapter watchlistAdapter,
        IJellyfinApi jellyfinApi)
    {
        _syncService = syncService;
        _configurationAccessor = configurationAccessor;
        _stateStore = stateStore;
        _watchlistAdapter = watchlistAdapter;
        _jellyfinApi = jellyfinApi;
    }

    [HttpPost("TestConnection")]
    public async Task<ActionResult<SeerrConnectionTestResult>> TestConnection([FromBody] SeerrConnectionTestRequest? request, CancellationToken cancellationToken)
    {
        if (request is not null)
        {
            return Ok(await _syncService.TestConnectionAsync(request.SeerrBaseUrl, request.ApiKey, cancellationToken).ConfigureAwait(false));
        }

        return Ok(await _syncService.TestConnectionAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("PreviewSync")]
    public async Task<ActionResult<SyncExecutionResult>> PreviewSync(CancellationToken cancellationToken)
        => Ok(await _syncService.PreviewAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("RunSync")]
    public async Task<ActionResult<SyncExecutionResult>> RunSync(CancellationToken cancellationToken)
        => Ok(await _syncService.RunAsync(Configuration.SyncRunMode.Manual, cancellationToken).ConfigureAwait(false));

    [HttpGet("Users")]
    public async Task<ActionResult<IReadOnlyList<UserSettingsDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        var configuredUsers = configuration.Users.ToDictionary(static user => user.JellyfinUserId, StringComparer.OrdinalIgnoreCase);
        var jellyfinUsers = await _jellyfinApi.GetUsersAsync(cancellationToken).ConfigureAwait(false);

        var users = jellyfinUsers.Select(user =>
        {
            var settings = configuredUsers.TryGetValue(user.Id, out var existing)
                ? existing
                : new Configuration.UserSyncSettings
                {
                    JellyfinUserId = user.Id,
                    JellyfinUserName = user.Name
                };

            settings.JellyfinUserId = user.Id;
            settings.JellyfinUserName = user.Name;

            return new UserSettingsDto
            {
                JellyfinUserId = user.Id,
                JellyfinUserName = user.Name,
                Settings = settings
            };
        }).ToList();

        return Ok(users);
    }

    [HttpGet("Status")]
    public async Task<ActionResult<PluginStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var compatibility = await _watchlistAdapter.CheckCompatibilityAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new PluginStatusDto
        {
            IsEnabled = _configurationAccessor.GetConfiguration().IsEnabled,
            LastSyncUtc = state.LastSyncUtc?.ToString("O") ?? string.Empty,
            CompatibilityOk = compatibility.IsCompatible,
            CompatibilityMessage = compatibility.Message
        });
    }
}
