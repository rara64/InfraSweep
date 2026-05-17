namespace InfraSweep.Tests;

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using InfraSweep.Discovery;
using InfraSweep.Discovery.Probes;
using InfraSweep.Discovery.Probes.Network;
using InfraSweep.Discovery.Probes.Services;
using Rssdp;
using Zeroconf;

public class InfraSweepDiscoveryTests
{
    private async Task TestTCPNetworkProbe()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();


        List<int> ports = await TcpProbe.GetOpenPorts(IPAddress.Parse(""), Enumerable.Range(1, 1001).ToArray());
        stopwatch.Stop();

        Console.WriteLine(JsonSerializer.Serialize(ports).ToString());
        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    //[Fact]
    private async Task TestARPNetworkProbe()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        List<(IPAddress,PhysicalAddress)> hosts = await ArpProbe.GetActiveHosts(IPAddress.Parse(""), IPAddress.Parse(""));

        stopwatch.Stop();

        foreach ((IPAddress host, PhysicalAddress address) in hosts)
            Console.WriteLine(host.ToString() + " | " + address.ToString());
        
        Console.WriteLine(hosts.Count);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);

    }

    [Fact]
    public async Task TestSsdpNetworkProbe()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        List<SsdpDevice> devices = await SsdpProbe.DiscoverDevices(IPAddress.Parse(""));

        stopwatch.Stop();

        foreach (SsdpDevice device in devices)
            Console.WriteLine(device.FriendlyName + " | " + device.Manufacturer);
        
        Console.WriteLine(devices.Count());

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestMdnsNetworkProbe()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        IReadOnlyList<IZeroconfHost> devices = await MdnsProbe.DiscoverDevices();

        stopwatch.Stop();

        foreach (IZeroconfHost device in devices)
            Console.WriteLine(device.IPAddress + " | " + device.Services.First().Value.Port + " | " + device.Services.First().Value.ServiceName);
        
        Console.WriteLine(devices.Count());

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestHttpServiceProbe()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        HttpProbeResult result = await HttpProbe.ProbeHTTPService(IPAddress.Parse(""), 80);

        stopwatch.Stop();

        Console.WriteLine(result.serverHeader);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }
}
