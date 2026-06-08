using System.Net;
using System.Net.NetworkInformation;
using Zeroconf;

namespace InfraSweep.Discovery.Probes.Network;

public class MdnsProbeResult
{
    public required IPAddress DeviceAddress { get; set; }
    public required string DisplayName { get; set; }
    public required int[] Ports { get; set; }
}

public class MdnsProbe
{
    public static async Task<List<MdnsProbeResult>> DiscoverDevices(NetworkInterface? netInterface = null, CancellationToken token = default)
    {
        ILookup<string, string> domains = await ZeroconfResolver.BrowseDomainsAsync(
            scanTime: TimeSpan.FromSeconds(10),
            netInterfacesToSendRequestOn: [netInterface],
            cancellationToken: token);

        if (domains.Count == 0)
            return [];

        IReadOnlyList<IZeroconfHost> hosts =
            await ZeroconfResolver.ResolveAsync(
                domains.Select(d => d.Key),
                netInterfacesToSendRequestOn: [netInterface],
                cancellationToken: token);

        return [.. hosts
            .GroupBy(device => device.IPAddress)
            .Select(group => new MdnsProbeResult
            {
                DeviceAddress = IPAddress.Parse(group.Key),
                DisplayName = group.First().DisplayName,
                Ports = [.. group
                    .SelectMany(device => device.Services.Values.Select(service => service.Port))
                    .Distinct()]
            })];
    }
}