using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using InfraSweep.Discovery.Probes.Network;
using InfraSweep.Discovery.Probes.Services;

namespace InfraSweep.Discovery;

public class DiscoveredHost
{
    public required string Address { get; set; }
    public string? MacAddress { get; set; }
    public List<ServiceInfo>? HostedServices { get; set; } = [];
    public string? DisplayName { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelName { get; set; }
    public string? ModelDescription { get; set; }
}

public class ServiceInfo
{
    public int Port { get; set; }
    public string? Banner { get; set; }
}

public class NetworkScanner
{
    private static readonly TokenBucketRateLimiter RateLimiter = 
        new(new TokenBucketRateLimiterOptions()
        {
            TokenLimit = 32,
            TokensPerPeriod = 32,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true
        });
        
    public static async Task<List<DiscoveredHost>> DiscoverHosts(CancellationToken token = default, Action<int>? progressCallback = null)
    {
        var validUnicastAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(netInterface => netInterface.OperationalStatus == OperationalStatus.Up)
            .Where(netInterface => netInterface.NetworkInterfaceType
                is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .Select(netInterface => (netInterface: netInterface, props: netInterface.GetIPProperties()))
            .Where(t => t.props.GatewayAddresses.Count > 0)
            .SelectMany(t => t.props.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => (netInterface: t.netInterface, addressInfo: address)))
            .ToList();

        List<DiscoveredHost> results = [];
        int totalCount = validUnicastAddresses.Count;

        if (totalCount == 0)
            progressCallback?.Invoke(100);

        int currentItem = 0;
        int lastProgress = 0;

        foreach ((NetworkInterface netInterface, UnicastIPAddressInformation addressInfo) in validUnicastAddresses)
        {
            if (token.IsCancellationRequested)
                break;

            ConcurrentBag<DiscoveredHost> networkHosts = [];

            IpRange ipRange = GetIpRangeFromAddressInfo(addressInfo);

            var (mdnsLookup, ssdpLookup) = await RunDiscoveryProbes(addressInfo.Address, netInterface, token);

            var prediscoveredPorts = mdnsLookup
                .Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Ports))
                .Concat(ssdpLookup.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Ports)))
                .GroupBy(pair => pair.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .SelectMany(pair => pair.Value)
                        .Distinct()
                        .ToArray());

            var allIps = ipRange.GetEnumerable();
            int currentIp = 0;
            int totalIpCount = allIps.Count();

            using SemaphoreSlim globalPortScanBudget = GetSemaphoreGlobalBudget(netInterface);

            void UpdateProgress(int completedIps)
            {
                double currentProgress = (currentItem + ((double)completedIps / totalIpCount)) / totalCount * 100;

                int oldProgress = Volatile.Read(ref lastProgress);

                if (oldProgress < (int)currentProgress)
                    if (oldProgress == Interlocked.CompareExchange(ref lastProgress, oldProgress, (int)currentProgress))
                        progressCallback?.Invoke((int)currentProgress);
            }

            await Parallel.ForEachAsync(allIps, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 64 }, async (address, ct) =>
            {
                await RateLimiter.AcquireAsync(1, ct);

                PhysicalAddress? physicalAddress = await ArpProbe.GetPhysicalAddress(address);

                if (physicalAddress == null)
                {
                    UpdateProgress(Interlocked.Increment(ref currentIp));
                    return;
                }

                prediscoveredPorts.TryGetValue(address, out var ports);
                List<ServiceInfo> serviceInfos = await GetOpenPortsAndCollectServiceInfo(address, globalPortScanBudget, ports, ct);

                mdnsLookup.TryGetValue(address, out var mdnsDevice);
                ssdpLookup.TryGetValue(address, out var ssdpDevice);
                
                networkHosts.Add(new DiscoveredHost()
                    {
                        Address = address.ToString(),
                        MacAddress = physicalAddress?.ToString(),
                        HostedServices = serviceInfos.ToList() ?? [],

                        DisplayName =
                            mdnsDevice?.DisplayName ??
                            ssdpDevice?.DisplayName,

                        Manufacturer = ssdpDevice?.Manufacturer,
                        ModelName = ssdpDevice?.ModelName,
                        ModelDescription = ssdpDevice?.ModelDescription
                    });

                UpdateProgress(Interlocked.Increment(ref currentIp));
            });

            results.AddRange(networkHosts);
            currentItem++;
        }

        return results;
    }

    private static SemaphoreSlim GetSemaphoreGlobalBudget(NetworkInterface netInterface)
    {
        int cpuBudget = Environment.ProcessorCount * 128;
        bool isWifi = netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
        int maxBudget = 512;

        long speedBps = netInterface.Speed;

        int speedLimit = speedBps switch
        {
            0 => 512,
            10_000_000 => 128, // 10M
            100_000_000 => 256, // 100M
            1_000_000_000 => 512, // 1G
            _ => maxBudget
        };

        int calculatedBudget = isWifi
            ? 256
            : Math.Min(speedLimit, cpuBudget);

        Console.WriteLine(calculatedBudget);

        return new SemaphoreSlim(calculatedBudget);
    }

    private static async Task<(
        Dictionary<IPAddress, MdnsProbeResult>,
        Dictionary<IPAddress, SsdpProbeResult>
    )> RunDiscoveryProbes(IPAddress hostAddress, NetworkInterface netInterface, CancellationToken token = default)
    {
        var mdnsDiscovery = MdnsProbe.DiscoverDevices(netInterface, token);
        var ssdpDiscovery = SsdpProbe.DiscoverDevices(hostAddress, token);

        await Task.WhenAll(mdnsDiscovery, ssdpDiscovery);

        return (
            mdnsDiscovery.Result.ToDictionary(device => device.DeviceAddress),
            ssdpDiscovery.Result.ToDictionary(device => device.DeviceAddress)
        );
    }

    private static async Task<List<ServiceInfo>> GetOpenPortsAndCollectServiceInfo(
        IPAddress address,
        SemaphoreSlim globalPortScanBudget,
        int[]? prediscoveredPorts,
        CancellationToken token = default)
    {
        List<ServiceInfo> foundServices = [];

        List<int> ports = [.. await TcpProbe.GetOpenPorts(address, KnownPorts.Top1000, globalPortScanBudget, token)];
        
        ports = [.. ports.Union(prediscoveredPorts ?? [])];

        foreach (int port in ports)
        {
            ServiceInfo banner = await GrabServiceBanners(address, port, token);
            foundServices.Add(banner);
        }

        return foundServices;
    }

    private static readonly Dictionary<int, Func<IPAddress, int, CancellationToken, Task<string?>>> serviceProbes = new()
    {
        [53] = DnsProbe.ProbeServerBanner,
        [21] = FtpProbe.ProbeServerBanner,
        [22] = SshProbe.ProbeServerBanner,
        [80] = HttpProbe.ProbeServerBanner,
    };

    private static async Task<ServiceInfo> GrabServiceBanners(
        IPAddress address, 
        int port, 
        CancellationToken token = default)
    {
        var prioritizedProbes = serviceProbes
            .OrderByDescending(pair => pair.Key == port)
            .Select(pair => pair.Value);

        foreach (var probe in prioritizedProbes)
        {
            var result = await probe(address, port, token);
            if (result != null)
                return new ServiceInfo()
                {
                    Port = port,
                    Banner = result
                };
        }

        return new ServiceInfo()
        {
            Port = port
        };
    }

    private static IpRange GetIpRangeFromAddressInfo(UnicastIPAddressInformation addressInfo)
    {
        byte[] ipBytes = addressInfo.Address.GetAddressBytes();
        byte[] maskBytes = addressInfo.IPv4Mask.GetAddressBytes();

        int ip = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(ipBytes, 0));

        int mask = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(maskBytes, 0));

        int netIp = ip & mask;
        int broadcastIp = ip | ~mask;

        IPAddress firstAddress = new IPAddress(BitConverter.GetBytes(
            IPAddress.HostToNetworkOrder(netIp + 1)));

        IPAddress lastAddress = new IPAddress(BitConverter.GetBytes(
            IPAddress.HostToNetworkOrder(broadcastIp - 1)));

        return new IpRange()
        {
            FirstAddress = firstAddress,
            LastAddress = lastAddress
        };
    }
}