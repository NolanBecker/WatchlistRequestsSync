using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class SonarrClient : ArrClientBase
{
    public SonarrClient(HttpClient httpClient, IPluginConfigurationAccessor configurationAccessor)
        : base(httpClient, configurationAccessor)
    {
    }

    public override ArrSourceKind Source => ArrSourceKind.Sonarr;

    protected override string GetBaseUrl(PluginConfiguration configuration) => configuration.SonarrBaseUrl;

    protected override string GetApiKey(PluginConfiguration configuration) => configuration.SonarrApiKey;

    protected override string GetConfiguredTags(PluginConfiguration configuration) => configuration.SonarrTags;

    protected override string ItemsEndpoint => "/api/v3/series";

    protected override ArrMediaItem MapItem(JsonElement item, IReadOnlyDictionary<int, string> tags)
    {
        var tagIds = ReadTagIds(item);
        return new ArrMediaItem
        {
            Source = Source,
            SourceItemId = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            MediaKind = MediaKind.Series,
            Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            Year = item.TryGetProperty("year", out var year) && year.ValueKind == JsonValueKind.Number ? year.GetInt32() : null,
            ProviderIds = new ProviderIdSet
            {
                Tvdb = item.TryGetProperty("tvdbId", out var tvdb) ? tvdb.ToString() : string.Empty,
                Tmdb = item.TryGetProperty("tmdbId", out var tmdb) ? tmdb.ToString() : string.Empty,
                Imdb = item.TryGetProperty("imdbId", out var imdb) ? imdb.GetString() ?? string.Empty : string.Empty
            },
            TagIds = tagIds,
            TagLabels = tagIds.Select(idValue => tags.TryGetValue(idValue, out var label) ? label : idValue.ToString()).ToArray()
        };
    }
}

public sealed class RadarrClient : ArrClientBase
{
    public RadarrClient(HttpClient httpClient, IPluginConfigurationAccessor configurationAccessor)
        : base(httpClient, configurationAccessor)
    {
    }

    public override ArrSourceKind Source => ArrSourceKind.Radarr;

    protected override string GetBaseUrl(PluginConfiguration configuration) => configuration.RadarrBaseUrl;

    protected override string GetApiKey(PluginConfiguration configuration) => configuration.RadarrApiKey;

    protected override string GetConfiguredTags(PluginConfiguration configuration) => configuration.RadarrTags;

    protected override string ItemsEndpoint => "/api/v3/movie";

    protected override ArrMediaItem MapItem(JsonElement item, IReadOnlyDictionary<int, string> tags)
    {
        var tagIds = ReadTagIds(item);
        return new ArrMediaItem
        {
            Source = Source,
            SourceItemId = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            MediaKind = MediaKind.Movie,
            Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            Year = item.TryGetProperty("year", out var year) && year.ValueKind == JsonValueKind.Number ? year.GetInt32() : null,
            ProviderIds = new ProviderIdSet
            {
                Tmdb = item.TryGetProperty("tmdbId", out var tmdb) ? tmdb.ToString() : string.Empty,
                Imdb = item.TryGetProperty("imdbId", out var imdb) ? imdb.GetString() ?? string.Empty : string.Empty
            },
            TagIds = tagIds,
            TagLabels = tagIds.Select(idValue => tags.TryGetValue(idValue, out var label) ? label : idValue.ToString()).ToArray()
        };
    }
}

public abstract class ArrClientBase : IArrClient
{
    private readonly HttpClient _httpClient;
    private readonly IPluginConfigurationAccessor _configurationAccessor;

    protected ArrClientBase(HttpClient httpClient, IPluginConfigurationAccessor configurationAccessor)
    {
        _httpClient = httpClient;
        _configurationAccessor = configurationAccessor;
    }

    public abstract ArrSourceKind Source { get; }

    protected abstract string GetBaseUrl(PluginConfiguration configuration);

    protected abstract string GetApiKey(PluginConfiguration configuration);

    protected abstract string GetConfiguredTags(PluginConfiguration configuration);

    protected abstract string ItemsEndpoint { get; }

    public Task<ArrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        return TestConnectionAsync(GetBaseUrl(configuration), GetApiKey(configuration), cancellationToken);
    }

    public async Task<ArrConnectionTestResult> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        var normalizedUrl = NormalizeBaseUrl(baseUrl);
        if (normalizedUrl is null)
        {
            return new ArrConnectionTestResult
            {
                IsSuccess = false,
                Message = $"{Source} base URL is invalid.",
                NormalizedBaseUrl = baseUrl
            };
        }

        using var request = CreateRequest(HttpMethod.Get, normalizedUrl + "/api/v3/system/status", apiKey);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return new ArrConnectionTestResult
            {
                IsSuccess = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode
                    ? "Connection succeeded."
                    : $"Connection failed with status {(int)response.StatusCode}.",
                NormalizedBaseUrl = normalizedUrl
            };
        }
        catch (Exception ex)
        {
            return new ArrConnectionTestResult
            {
                IsSuccess = false,
                Message = $"Connection failed: {ex.Message}",
                NormalizedBaseUrl = normalizedUrl
            };
        }
    }

    public Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        return GetTaggedItemsAsync(GetBaseUrl(configuration), GetApiKey(configuration), GetConfiguredTags(configuration), cancellationToken);
    }

    public async Task<IReadOnlyList<ArrMediaItem>> GetTaggedItemsAsync(string baseUrl, string apiKey, string configuredTags, CancellationToken cancellationToken)
    {
        var normalizedUrl = NormalizeBaseUrl(baseUrl)
            ?? throw new InvalidOperationException($"{Source} base URL is invalid.");
        var configuredTagValues = ParseConfiguredTags(configuredTags);
        if (configuredTagValues.Count == 0)
        {
            return Array.Empty<ArrMediaItem>();
        }

        var tags = await GetTagsAsync(normalizedUrl, apiKey, cancellationToken).ConfigureAwait(false);
        var matchingTagIds = ResolveMatchingTagIds(configuredTagValues, tags);
        if (matchingTagIds.Count == 0)
        {
            return Array.Empty<ArrMediaItem>();
        }

        using var request = CreateRequest(HttpMethod.Get, normalizedUrl + ItemsEndpoint, apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ArrMediaItem>();
        }

        var results = new List<ArrMediaItem>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var mapped = MapItem(item, tags);
            if (mapped.SourceItemId <= 0 || string.IsNullOrWhiteSpace(mapped.Title))
            {
                continue;
            }

            if (mapped.TagIds.Any(matchingTagIds.Contains))
            {
                results.Add(mapped);
            }
        }

        return results;
    }

    protected abstract ArrMediaItem MapItem(JsonElement item, IReadOnlyDictionary<int, string> tags);

    protected static int[] ReadTagIds(JsonElement item)
    {
        if (!item.TryGetProperty("tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        return tagsElement
            .EnumerateArray()
            .Where(static value => value.ValueKind == JsonValueKind.Number)
            .Select(static value => value.GetInt32())
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<int, string>> GetTagsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl + "/api/v3/tag", apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<int, string>();
        }

        return document.RootElement
            .EnumerateArray()
            .Where(static item => item.TryGetProperty("id", out _) && item.TryGetProperty("label", out _))
            .ToDictionary(
                static item => item.GetProperty("id").GetInt32(),
                static item => item.GetProperty("label").GetString() ?? string.Empty);
    }

    private static HashSet<string> ParseConfiguredTags(string configuredTags)
        => configuredTags
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<int> ResolveMatchingTagIds(IReadOnlySet<string> configuredTags, IReadOnlyDictionary<int, string> tags)
    {
        var matches = new HashSet<int>();
        foreach (var (id, label) in tags)
        {
            if (configuredTags.Contains(label) || configuredTags.Contains(id.ToString(CultureInfo.InvariantCulture)))
            {
                matches.Add(id);
            }
        }

        return matches;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string apiKey)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return request;
    }

    private static string? NormalizeBaseUrl(string input)
    {
        input = input?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }
}
