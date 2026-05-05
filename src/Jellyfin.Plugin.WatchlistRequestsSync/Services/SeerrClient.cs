using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Services;

public sealed class SeerrClient : ISeerrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IPluginConfigurationAccessor _configurationAccessor;

    public SeerrClient(HttpClient httpClient, IPluginConfigurationAccessor configurationAccessor)
    {
        _httpClient = httpClient;
        _configurationAccessor = configurationAccessor;
    }

    public async Task<SeerrConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        var normalizedUrl = NormalizeBaseUrl(configuration.SeerrBaseUrl);
        if (normalizedUrl is null)
        {
            return new SeerrConnectionTestResult
            {
                IsSuccess = false,
                Message = "Seerr/Jellyseerr base URL is invalid.",
                NormalizedBaseUrl = configuration.SeerrBaseUrl
            };
        }

        using var request = CreateRequest(HttpMethod.Get, normalizedUrl + "/api/v1/status", configuration.ApiKey);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return new SeerrConnectionTestResult
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
            return new SeerrConnectionTestResult
            {
                IsSuccess = false,
                Message = $"Connection failed: {ex.Message}",
                NormalizedBaseUrl = normalizedUrl
            };
        }
    }

    public async Task<IReadOnlyList<NormalizedSeerrRequest>> GetRequestsAsync(CancellationToken cancellationToken)
    {
        var configuration = _configurationAccessor.GetConfiguration();
        var normalizedUrl = NormalizeBaseUrl(configuration.SeerrBaseUrl)
            ?? throw new InvalidOperationException("Seerr/Jellyseerr base URL is invalid.");

        using var request = CreateRequest(HttpMethod.Get, normalizedUrl + "/api/v1/request?take=500&skip=0&sort=added", configuration.ApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<NormalizedSeerrRequest>();
        }

        var normalized = new List<NormalizedSeerrRequest>();
        foreach (var result in results.EnumerateArray())
        {
            var media = result.TryGetProperty("media", out var mediaElement) ? mediaElement : default;
            var requestedBy = result.TryGetProperty("requestedBy", out var userElement) ? userElement : default;

            var mediaType = ParseMediaType(media);
            normalized.Add(new NormalizedSeerrRequest
            {
                RequestId = result.TryGetProperty("id", out var idElement) ? idElement.GetInt64() : 0,
                SeerrUserId = requestedBy.TryGetProperty("id", out var seerrUserId) ? seerrUserId.GetInt64() : 0,
                JellyfinUserId = TryGetJellyfinUserId(requestedBy),
                RequestingUserName = requestedBy.TryGetProperty("displayName", out var displayName)
                    ? displayName.GetString() ?? string.Empty
                    : requestedBy.TryGetProperty("username", out var userName)
                        ? userName.GetString() ?? string.Empty
                        : string.Empty,
                MediaType = mediaType,
                ApprovalState = ParseApprovalState(result),
                AvailabilityState = ParseAvailabilityState(media),
                ProviderIds = new ProviderIdSet
                {
                    Tmdb = media.TryGetProperty("tmdbId", out var tmdbId) ? tmdbId.ToString() : string.Empty,
                    Tvdb = media.TryGetProperty("tvdbId", out var tvdbId) ? tvdbId.ToString() : string.Empty,
                    Imdb = media.TryGetProperty("imdbId", out var imdbId) ? imdbId.GetString() ?? string.Empty : string.Empty
                },
                Title = media.TryGetProperty("title", out var title)
                    ? title.GetString() ?? string.Empty
                    : media.TryGetProperty("name", out var name)
                        ? name.GetString() ?? string.Empty
                        : string.Empty,
                Year = media.TryGetProperty("releaseDate", out var releaseDate) && DateTime.TryParse(releaseDate.GetString(), out var parsedDate)
                    ? parsedDate.Year
                    : media.TryGetProperty("firstAirDate", out var firstAirDate) && DateTime.TryParse(firstAirDate.GetString(), out var parsedAirDate)
                        ? parsedAirDate.Year
                        : null,
                IsSeasonSpecificRequest = result.TryGetProperty("seasons", out var seasons) && seasons.ValueKind == JsonValueKind.Array && seasons.GetArrayLength() > 0
            });
        }

        return normalized;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string apiKey)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return request;
    }

    private static string? NormalizeBaseUrl(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static RequestMediaType ParseMediaType(JsonElement media)
    {
        if (media.ValueKind == JsonValueKind.Object
            && media.TryGetProperty("mediaType", out var mediaType)
            && string.Equals(mediaType.GetString(), "tv", StringComparison.OrdinalIgnoreCase))
        {
            return RequestMediaType.Series;
        }

        return RequestMediaType.Movie;
    }

    private static RequestApprovalState ParseApprovalState(JsonElement request)
    {
        if (request.TryGetProperty("status", out var status))
        {
            return status.GetInt32() >= 2 ? RequestApprovalState.Approved : RequestApprovalState.Pending;
        }

        return RequestApprovalState.Unknown;
    }

    private static RequestAvailabilityState ParseAvailabilityState(JsonElement media)
    {
        if (!media.TryGetProperty("status", out var status))
        {
            return RequestAvailabilityState.Unknown;
        }

        return status.GetInt32() switch
        {
            >= 5 => RequestAvailabilityState.Available,
            4 => RequestAvailabilityState.PartiallyAvailable,
            3 => RequestAvailabilityState.Processing,
            _ => RequestAvailabilityState.Pending
        };
    }

    private static string TryGetJellyfinUserId(JsonElement requestedBy)
    {
        if (requestedBy.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (requestedBy.TryGetProperty("jellyfinUserId", out var jellyfinUserId))
        {
            return jellyfinUserId.GetString() ?? string.Empty;
        }

        if (requestedBy.TryGetProperty("jellyfinUser", out var jellyfinUser)
            && jellyfinUser.ValueKind == JsonValueKind.Object
            && jellyfinUser.TryGetProperty("id", out var id))
        {
            return id.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
