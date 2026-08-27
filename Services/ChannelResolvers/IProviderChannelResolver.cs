using NundoTv_WebAPI.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services.ChannelResolvers
{
    public interface IProviderChannelResolver
    {
        string ProviderKey { get; }
        Task<List<ChannelInfo>> ResolveChannelsAsync(string targetUrl, CancellationToken cancellationToken = default);
    }
}
