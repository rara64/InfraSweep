using Zeroconf;

namespace InfraSweep.Discovery;

public class MdnsProbe
{
    public static async Task<IReadOnlyList<IZeroconfHost>> DiscoverDevices()
    {
        ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync();
        return await ZeroconfResolver.ResolveAsync(domains.Select(d => d.Key));
    }
}