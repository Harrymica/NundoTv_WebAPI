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
    public class DaddyLiveResolver : IProviderChannelResolver
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DaddyLiveResolver> _logger;

        public string ProviderKey => "daddylive";

        public DaddyLiveResolver(IHttpClientFactory httpClientFactory, ILogger<DaddyLiveResolver> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<ChannelInfo>> ResolveChannelsAsync(string targetUrl, CancellationToken cancellationToken = default)
        {
            var channels = new List<ChannelInfo>();
            string fetchUrl = targetUrl;

            if (!fetchUrl.Contains("24-7-channels.php"))
            {
                fetchUrl = new Uri(new Uri(targetUrl), "/24-7-channels.php").ToString();
            }

            try
            {
                var client = _httpClientFactory.CreateClient("StreamScraper");
                string html = await client.GetStringAsync(fetchUrl, cancellationToken);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Find all anchor links targeting channel streams (e.g., stream-123.php)
                var anchorNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'stream-') or contains(@href, 'channel')]");

                if (anchorNodes != null)
                {
                    foreach (var node in anchorNodes)
                    {
                        string href = node.GetAttributeValue("href", "").Trim();
                        string rawName = node.InnerText.Trim();
                        string cleanName = Regex.Replace(rawName, @"\s+", " ").Trim();

                        if (string.IsNullOrWhiteSpace(cleanName) || cleanName.Equals("Click Here", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        Uri baseUri = new Uri(fetchUrl);
                        Uri absoluteUri = new Uri(baseUri, href);
                        string playableUrl = absoluteUri.ToString();

                        string channelId = ExtractChannelId(href);

                        if (!channels.Any(c => c.PlayableUrl.Equals(playableUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            channels.Add(new ChannelInfo
                            {
                                ChannelId = channelId,
                                ChannelName = cleanName,
                                PlayableUrl = playableUrl,
                                Category = "DaddyLive 24/7 Channels"
                            });
                        }
                    }
                }

                // Regex Fallback if DOM selector found nothing
                if (channels.Count == 0)
                {
                    string pattern = @"<a\s+[^>]*href=[""']([^""']*(?:stream-|channel)[^""']*)[""'][^>]*>(.*?)</a>";
                    MatchCollection matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    foreach (Match match in matches)
                    {
                        string href = match.Groups[1].Value.Trim();
                        string rawText = Regex.Replace(match.Groups[2].Value, @"<[^>]+>", "").Trim();
                        string cleanName = Regex.Replace(rawText, @"\s+", " ").Trim();

                        if (string.IsNullOrWhiteSpace(cleanName)) continue;

                        Uri baseUri = new Uri(fetchUrl);
                        Uri absoluteUri = new Uri(baseUri, href);
                        string playableUrl = absoluteUri.ToString();
                        string channelId = ExtractChannelId(href);

                        if (!channels.Any(c => c.PlayableUrl.Equals(playableUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            channels.Add(new ChannelInfo
                            {
                                ChannelId = channelId,
                                ChannelName = cleanName,
                                PlayableUrl = playableUrl,
                                Category = "DaddyLive 24/7 Channels"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve channels for DaddyLive at {Url}", targetUrl);
            }

            return channels;
        }

        private static string ExtractChannelId(string href)
        {
            var match = Regex.Match(href, @"stream-([0-9A-Za-z_-]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return href.Trim('/');
        }
    }
}
