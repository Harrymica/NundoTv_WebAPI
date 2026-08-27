using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using NundoTv_WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services.ChannelResolvers
{
    public class Score808Resolver : IProviderChannelResolver
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Score808Resolver> _logger;

        public string ProviderKey => "score808";

        public Score808Resolver(IHttpClientFactory httpClientFactory, ILogger<Score808Resolver> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<ChannelInfo>> ResolveChannelsAsync(string targetUrl, CancellationToken cancellationToken = default)
        {
            var channels = new List<ChannelInfo>();
            try
            {
                var client = _httpClientFactory.CreateClient("StreamScraper");
                string fetchUrl = targetUrl;

                if (!fetchUrl.Contains("/links/") && !fetchUrl.EndsWith("/football"))
                {
                    fetchUrl = fetchUrl.TrimEnd('/') + "/football";
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");

                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) return channels;

                string html = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Scenario A: targetUrl is a main directory page (e.g. /football) -> extract match links
                if (!targetUrl.Contains("/links/"))
                {
                    var matchLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/links/')]");
                    if (matchLinks != null)
                    {
                        foreach (var node in matchLinks)
                        {
                            string href = node.GetAttributeValue("href", "").Trim();
                            string text = Regex.Replace(node.InnerText.Trim(), @"\s+", " ");
                            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(text)) continue;

                            Uri baseUri = new Uri(fetchUrl);
                            string fullUrl = new Uri(baseUri, href).ToString();
                            string matchSlug = href.Split(new[] { "/links/" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? href;

                            if (!channels.Any(c => c.PlayableUrl.Equals(fullUrl, StringComparison.OrdinalIgnoreCase)))
                            {
                                channels.Add(new ChannelInfo
                                {
                                    ChannelId = matchSlug,
                                    ChannelName = text,
                                    PlayableUrl = fullUrl,
                                    Category = "Score808 Live Matches"
                                });
                            }
                        }
                    }
                }
                else
                {
                    // Scenario B: targetUrl is a specific match page (/links/barcelona-vs-athletic-bilbao) -> extract stream links (hitcast, embed, etc)
                    var streamNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'hitcast.st') or contains(@href, 'totwatch') or contains(@href, 'totview') or contains(@href, 'embed') or contains(@href, 'streame') or contains(@href, 'vivtops') or contains(@href, 'daddylive')]");
                    if (streamNodes != null)
                    {
                        int idx = 1;
                        foreach (var node in streamNodes)
                        {
                            string href = node.GetAttributeValue("href", "").Trim();
                            string text = node.InnerText.Trim();
                            if (string.IsNullOrWhiteSpace(href) || href.StartsWith("#")) continue;

                            if (string.IsNullOrWhiteSpace(text) || text.Equals("Watch", StringComparison.OrdinalIgnoreCase))
                            {
                                text = $"Stream Server #{idx}";
                            }

                            if (!channels.Any(c => c.PlayableUrl.Equals(href, StringComparison.OrdinalIgnoreCase)))
                            {
                                channels.Add(new ChannelInfo
                                {
                                    ChannelId = $"score808-stream-{idx}",
                                    ChannelName = text,
                                    PlayableUrl = href,
                                    Category = "Score808 Streams"
                                });
                                idx++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed resolving channels for Score808 provider at {Url}", targetUrl);
            }

            return channels;
        }
    }
}
