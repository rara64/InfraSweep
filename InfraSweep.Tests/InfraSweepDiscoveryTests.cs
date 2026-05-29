namespace InfraSweep.Tests;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using InfraSweep.Discovery;
using InfraSweep.Discovery.Probes;
using InfraSweep.Discovery.Probes.Network;
using InfraSweep.Discovery.Probes.Services;
using InfraSweep.Analysis;
using Rssdp;
using Zeroconf;
using System.Runtime.InteropServices.Marshalling;

public class InfraSweepDiscoveryTests
{
    [Fact]
    public async Task TestTCPNetworkProbe()
    {
        return;
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
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        IpRange ipRange = new()
        {
            FirstAddress = IPAddress.Parse(""),
            LastAddress = IPAddress.Parse("")
        };

        //Dictionary<IPAddress, PhysicalAddress> hosts = await ArpProbe.GetActiveHosts(ipRange);

        stopwatch.Stop();

        //foreach ((IPAddress host, PhysicalAddress address) in hosts)
        //    Console.WriteLine(host.ToString() + " | " + address.ToString());

        //Console.WriteLine(hosts.Count);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);

    }

    [Fact]
    public async Task TestSsdpNetworkProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        List<SsdpProbeResult> devices = await SsdpProbe.DiscoverDevices(IPAddress.Parse(""));

        stopwatch.Stop();

        Console.WriteLine(devices.Count);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestMdnsNetworkProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        List<MdnsProbeResult> devices = await MdnsProbe.DiscoverDevices();

        stopwatch.Stop();

        Console.WriteLine(devices.Count);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    //[Fact]
    private async Task TestHttpServiceProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        //string? serverHeader = await HttpProbe.ProbeServerHeader(IPAddress.Parse(""), 80);

        stopwatch.Stop();

        //Console.WriteLine(serverHeader);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestFTPServiceProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string? serverHeader = await FtpProbe.ProbeServerBanner(IPAddress.Parse(""));

        stopwatch.Stop();

        Console.WriteLine(serverHeader);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestSshServiceProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string? serverHeader = await SshProbe.ProbeServerBanner(IPAddress.Parse(""));

        stopwatch.Stop();

        Console.WriteLine(serverHeader);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestDnsServiceProbe()
    {
        return;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string? serverHeader = await DnsProbe.ProbeServerBanner(IPAddress.Parse(""));

        stopwatch.Stop();

        Console.WriteLine(serverHeader);

        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task TestScannerDiscovery()
    {
        //return;
        //List<DiscoveredHost> r = await NetworkScanner.DiscoverHosts();
        //Console.WriteLine(JsonSerializer.Serialize(r));
        //return;

        //CancellationTokenSource cts = new();
        //cts.CancelAfter(TimeSpan.FromSeconds(5));

        string resultString = File.ReadAllText("../../../scanresults.json");
        List<DiscoveredHost> results = JsonSerializer.Deserialize<List<DiscoveredHost>>(resultString);
        List<AnalyzedHost> results2 = await NetworkAnalysis.AnalyzeDiscoveredHosts(results);
        foreach (AnalyzedHost host in results2)
        {
            if (!string.IsNullOrEmpty(host.HostCpe))
            {
                VulnerabilityPage? page = await VulnerabilityResolver.GetVulnerabilities(host.HostCpe);
                Console.WriteLine(page?.Cves?.FirstOrDefault()?.CveId);   
            }

            foreach (AnalyzedServiceInfo info in host.Services)
            {
                foreach (IdentifiedSoftware soft in info.IdentifiedSoftware)
                {
                    if (soft.MatchedCpes != null)
                        foreach (string cpe in soft.MatchedCpes)
                        {
                            if (string.IsNullOrWhiteSpace(cpe))
                                continue;
                            
                            VulnerabilityPage? softPage = await VulnerabilityResolver.GetVulnerabilities(cpe, soft.Version);
                            Console.WriteLine($"SOFT ({cpe}:{soft.Version}) => {softPage?.Cves?.FirstOrDefault()?.CveId}");
                        }
                }
            }
        }
    }
}
