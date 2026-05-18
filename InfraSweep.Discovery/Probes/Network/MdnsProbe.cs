using Zeroconf;

namespace InfraSweep.Discovery;

public class MdnsProbe
{
    public static async Task<List<IZeroconfHost>> DiscoverDevices()
    {
        List<IZeroconfHost> discoveredHosts = [];

        for (int i = 0; i < 2; i++)
        {
            ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync();

            IReadOnlyList<IZeroconfHost> hosts = 
                await ZeroconfResolver.ResolveAsync(domains.Select(d => d.Key));   
            
            discoveredHosts.AddRange(hosts);
        }

        return discoveredHosts
            .DistinctBy(host => host.IPAddress)
            .ToList();
    }
}