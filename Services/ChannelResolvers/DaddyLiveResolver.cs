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
                        string channelPageUrl = absoluteUri.ToString();
                        string channelId = ExtractChannelId(href);

                        if (!channels.Any(c => c.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                        {
                            channels.Add(new ChannelInfo
                            {
                                ChannelId = channelId,
                                ChannelName = cleanName,
                                PlayableUrl = channelPageUrl,
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
                        string channelPageUrl = absoluteUri.ToString();
                        string channelId = ExtractChannelId(href);

                        if (!channels.Any(c => c.ChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase)))
                        {
                            channels.Add(new ChannelInfo
                            {
                                ChannelId = channelId,
                                ChannelName = cleanName,
                                PlayableUrl = channelPageUrl,
                                Category = "DaddyLive 24/7 Channels"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve channel list for DaddyLive at {Url}", targetUrl);
            }

            return channels;
        }

        /// <summary>
        /// Resolves a DaddyLive channel ID or page URL to a direct ad-free .m3u8 HLS stream URL
        /// by performing 2-step iframe extraction & base64 decoding of the Clappr player script.
        /// </summary>
        public async Task<string?> ResolveDirectM3u8Async(string channelIdOrUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                string channelId = ExtractChannelId(channelIdOrUrl);
                var client = _httpClientFactory.CreateClient("StreamScraper");

                var domains = new[] { "https://dlhd.so", "https://daddylive.mp", "https://dlhd.sx", "https://dlstreams.st" };
                string? iframeSrc = null;
                string workingDomain = "https://dlhd.so";

                foreach (var domain in domains)
                {
                    try
                    {
                        string streamPageUrl = $"{domain}/stream/stream-{channelId}.php";
                        using var req = new HttpRequestMessage(HttpMethod.Get, streamPageUrl);
                        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
                        req.Headers.Referrer = new Uri($"{domain}/");

                        using var resp = await client.SendAsync(req, cancellationToken);
                        if (resp.IsSuccessStatusCode)
                        {
                            string html = await resp.Content.ReadAsStringAsync(cancellationToken);
                            var match = Regex.Match(html, @"iframe[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                iframeSrc = match.Groups[1].Value;
                                workingDomain = domain;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed fetching stream page for channel {ChannelId} from {Domain}", channelId, domain);
                    }
                }

                if (string.IsNullOrEmpty(iframeSrc))
                {
                    // Default fallback format for daddy.php embed
                    iframeSrc = $"https://hamis.romponalis.st/premiumtv/daddy.php?id={channelId}";
                }

                // Fetch iframe player embed HTML
                using var iframeReq = new HttpRequestMessage(HttpMethod.Get, iframeSrc);
                iframeReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
                iframeReq.Headers.Referrer = new Uri($"{workingDomain}/");

                using var iframeResp = await client.SendAsync(iframeReq, cancellationToken);
                if (!iframeResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("DaddyLive iframe embed page returned status: {Status}", iframeResp.StatusCode);
                    return null;
                }

                string embedHtml = await iframeResp.Content.ReadAsStringAsync(cancellationToken);

                // Extract base64 encoded m3u8 URL from window.atob('...')
                var b64Match = Regex.Match(embedHtml, @"window\.atob\s*\(\s*['""]([A-Za-z0-9+/=]+)['""]\s*\)", RegexOptions.IgnoreCase);
                if (b64Match.Success)
                {
                    string b64String = b64Match.Groups[1].Value;
                    byte[] bytes = Convert.FromBase64String(b64String);
                    string decodedUrl = System.Text.Encoding.UTF8.GetString(bytes).Trim();

                    if (decodedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Successfully resolved direct .m3u8 for DaddyLive channel {ChannelId}: {Url}", channelId, decodedUrl);
                        return decodedUrl;
                    }
                }

                _logger.LogWarning("Base64 stream URL pattern not found in DaddyLive embed script for channel {ChannelId}", channelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving direct .m3u8 stream for DaddyLive channel/URL: {Target}", channelIdOrUrl);
            }

            return null;
        }

        private static string ExtractChannelId(string href)
        {
            var match = Regex.Match(href, @"stream-([0-9A-Za-z_-]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return href.Trim('/').Split('/').LastOrDefault() ?? href;
        }
    }
}

