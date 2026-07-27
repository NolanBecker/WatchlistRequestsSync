using System.Net;
using System.Text;
using Jellyfin.Plugin.WatchlistRequestsSync.Configuration;
using Jellyfin.Plugin.WatchlistRequestsSync.Models;
using Jellyfin.Plugin.WatchlistRequestsSync.Services;
using Xunit;

namespace Jellyfin.Plugin.WatchlistRequestsSync.Tests;

public sealed class ArrClientTests
{
    [Fact]
    public async Task SonarrClient_MatchesConfiguredLabel_WhenTagIdsAreStrings()
    {
        var client = CreateSonarrClient(
            tagsJson: """
                [
                  { "id": "17", "label": "Watchlist" }
                ]
                """,
            itemsJson: """
                [
                  {
                    "id": 101,
                    "title": "Dark",
                    "year": 2017,
                    "tvdbId": 318408,
                    "tmdbId": 70523,
                    "imdbId": "tt5753856",
                    "tags": ["17"]
                  }
                ]
                """);

        var items = await client.GetTaggedItemsAsync("http://sonarr.local", "key", "  watchlist  ", CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(ArrSourceKind.Sonarr, item.Source);
        Assert.Equal("Dark", item.Title);
        Assert.Equal([17], item.TagIds);
        Assert.Equal(["Watchlist"], item.TagLabels);
    }

    [Fact]
    public async Task SonarrClient_ThrowsWhenConfiguredTagDoesNotExist()
    {
        var client = CreateSonarrClient(
            tagsJson: """
                [
                  { "id": 17, "label": "Existing Tag" }
                ]
                """,
            itemsJson: "[]");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetTaggedItemsAsync("http://sonarr.local", "key", "missing-tag", CancellationToken.None));

        Assert.Contains("Configured Sonarr tags were not found.", error.Message, StringComparison.Ordinal);
        Assert.Contains("Existing Tag", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SonarrClient_ThrowsWhenNoSeriesUseConfiguredTag()
    {
        var client = CreateSonarrClient(
            tagsJson: """
                [
                  { "id": 17, "label": "Watchlist" }
                ]
                """,
            itemsJson: """
                [
                  {
                    "id": 101,
                    "title": "Dark",
                    "year": 2017,
                    "tvdbId": 318408,
                    "tmdbId": 70523,
                    "imdbId": "tt5753856",
                    "tags": [18]
                  }
                ]
                """);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetTaggedItemsAsync("http://sonarr.local", "key", "Watchlist", CancellationToken.None));

        Assert.Contains("no Sonarr library items currently use them", error.Message, StringComparison.Ordinal);
    }

    private static SonarrClient CreateSonarrClient(string tagsJson, string itemsJson)
        => new(
            new HttpClient(new FakeHttpMessageHandler(tagsJson, itemsJson)),
            new FakeConfigurationAccessor());

    private sealed class FakeConfigurationAccessor : IPluginConfigurationAccessor
    {
        public PluginConfiguration GetConfiguration() => new();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _tagsJson;
        private readonly string _itemsJson;

        public FakeHttpMessageHandler(string tagsJson, string itemsJson)
        {
            _tagsJson = tagsJson;
            _itemsJson = itemsJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responseBody = request.RequestUri?.AbsolutePath switch
            {
                "/api/v3/tag" => _tagsJson,
                "/api/v3/series" => _itemsJson,
                _ => "[]"
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
