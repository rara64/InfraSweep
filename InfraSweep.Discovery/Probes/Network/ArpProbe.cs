using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.RateLimiting;
using System.Net.Sockets;
using ArpLookup;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace InfraSweep.Discovery.Probes.Network;

public class ArpProbe
{
    private static IEnumerable<IPAddress> GetIPRange(IPAddress firstAddress, IPAddress lastAddress)
    {
        int first = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(firstAddress.GetAddressBytes(), 0));

        int last = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(lastAddress.GetAddressBytes(), 0));

        if (last < first)
            throw new ArgumentException();

        for (int i = first; i <= last; i++)
        {
            yield return new IPAddress
                (BitConverter.GetBytes(IPAddress.HostToNetworkOrder(i)));
        }
    }

    public static async Task<List<(IPAddress, PhysicalAddress)>> GetActiveHosts
        (IPAddress firstAddress, IPAddress lastAddress, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(firstAddress);
        ArgumentNullException.ThrowIfNull(lastAddress);

        if (firstAddress.AddressFamily != AddressFamily.InterNetwork
            || lastAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException();

        ConcurrentBag<(IPAddress, PhysicalAddress)> activeHosts = new();

        using var rateLimiter = new TokenBucketRateLimiter(new(){
            TokenLimit = 32,
            TokensPerPeriod = 32,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 64,
            AutoReplenishment = true
        });

        ParallelOptions options = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = 64
        };

        await Parallel.ForEachAsync(GetIPRange(firstAddress, lastAddress), options,
            async (address, ct) =>
            {
                await rateLimiter.AcquireAsync(1, ct);

                await Task.Delay(10 + Random.Shared.Next(10));

                PhysicalAddress? macAddress;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    macAddress = await MacOSLookup(address);
                else
                    macAddress = await Arp.LookupAsync(address);
                
                if (macAddress != null)
                    activeHosts.Add((address, macAddress));
            });

        return activeHosts.ToList();
    }

    private static async Task<PhysicalAddress?> MacOSLookup(IPAddress address)
    {
        await new Ping().SendPingAsync(address.ToString(), timeout:2000);

        Process arpProcess = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = "-a",
                RedirectStandardOutput = true,
                UseShellExecute = false   
            }
        };
        arpProcess.Start();

        string arpOutput = await arpProcess.StandardOutput.ReadToEndAsync();
        await arpProcess.WaitForExitAsync();

        var match = Regex.Match(arpOutput, $@"\({Regex.Escape(address.ToString())}\)\s+at\s+(?<mac>[0-9a-f:]+)");

        if (!match.Success)
            return null;

        return new PhysicalAddress(
            match.Groups["mac"].Value
            .Split(":")
            .Select(part => Convert.ToByte(part, 16))
            .ToArray()
        );
    }
}