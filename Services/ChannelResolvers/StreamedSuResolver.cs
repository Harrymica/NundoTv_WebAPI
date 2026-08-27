using Microsoft.Extensions.Logging;
using NundoTv_WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services.ChannelResolvers
{
    public class StreamedSuResolver : IProviderChannelResolver
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StreamedSuResolver> _logger;

        public string ProviderKey => "streamed";

        public StreamedSuResolver(IHttpClientFactory httpClientFactory, ILogger<StreamedSuResolver> logger)
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
                string html = await client.GetStringAsync(targetUrl, cancellationToken);

                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/match/') or contains(@href, '/stream/')]");
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        string href = link.GetAttributeValue("href", "");
                        string text = link.InnerText.Trim();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        Uri baseUri = new Uri(targetUrl);
                        string fullUrl = new Uri(baseUri, href).ToString();

                        channels.Add(new ChannelInfo
                        {
                            ChannelId = href.Trim('/'),
                            ChannelName = text,
                            PlayableUrl = fullUrl,
                            Category = "Streamed Live Events"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving channels for Streamed provider at {Url}", targetUrl);
            }

            return channels;
        }
    }
}
