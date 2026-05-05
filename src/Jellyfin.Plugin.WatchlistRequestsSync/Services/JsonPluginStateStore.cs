using System.Text.Json;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class JsonPluginStateStore : IPluginStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath;

    public JsonPluginStateStore()
    {
        var configDir = Plugin.Instance?.DataFolderPath is { Length: > 0 } dataFolderPath
            ? dataFolderPath
            : AppContext.BaseDirectory;
        _statePath = Path.Combine(configDir, "WatchlistRequestsSync.state.json");
    }

    public async Task<PluginState> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_statePath))
            {
                return new PluginState();
            }

            await using var stream = File.OpenRead(_statePath);
            return await JsonSerializer.DeserializeAsync<PluginState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? new PluginState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(PluginState state, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath) ?? AppContext.BaseDirectory);
            await using var stream = File.Create(_statePath);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
