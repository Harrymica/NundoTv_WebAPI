using Microsoft.Extensions.Logging;
using NundoTv_WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services.ChannelResolvers
{
    public interface IChannelResolverService
    {
        Task<List<ChannelInfo>> ResolveChannelsAsync(StreamLink streamLink, CancellationToken cancellationToken = default);
    }

    public class ChannelResolverService : IChannelResolverService
    {
        private readonly IEnumerable<IProviderChannelResolver> _resolvers;
        private readonly ILogger<ChannelResolverService> _logger;

        public ChannelResolverService(
            IEnumerable<IProviderChannelResolver> resolvers,
            ILogger<ChannelResolverService> logger)
        {
            _resolvers = resolvers;
            _logger = logger;
        }

        public async Task<List<ChannelInfo>> ResolveChannelsAsync(StreamLink streamLink, CancellationToken cancellationToken = default)
        {
            if (!streamLink.RequiresChannelSearch || string.IsNullOrWhiteSpace(streamLink.ChannelResolverKey))
            {
                return GetFallbackChannels(streamLink);
            }

            var resolver = _resolvers.FirstOrDefault(r => r.ProviderKey.Equals(streamLink.ChannelResolverKey, StringComparison.OrdinalIgnoreCase));
            if (resolver == null)
            {
                _logger.LogWarning("No registered channel resolver found for key '{Key}'", streamLink.ChannelResolverKey);
                return GetFallbackChannels(streamLink);
            }

            var resolved = await resolver.ResolveChannelsAsync(streamLink.TargetUrl, cancellationToken);
            if (resolved == null || resolved.Count == 0)
            {
                _logger.LogInformation("Resolver '{Key}' returned 0 channels. Returning fallback base URL.", streamLink.ChannelResolverKey);
                return GetFallbackChannels(streamLink);
            }

            return resolved;
        }

        private static List<ChannelInfo> GetFallbackChannels(StreamLink streamLink)
        {
            return new List<ChannelInfo>
            {
                new ChannelInfo
                {
                    ChannelId = streamLink.Id.ToString(),
                    ChannelName = streamLink.SiteName,
                    PlayableUrl = streamLink.TargetUrl,
                    Category = streamLink.Category
                }
            };
        }
    }
}
