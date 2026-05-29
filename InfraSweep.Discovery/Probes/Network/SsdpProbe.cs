using System.Net;
using Rssdp;
using Polly;
using Polly.Retry;
using Polly.Fallback;

namespace InfraSweep.Discovery.Probes.Network;

public class SsdpProbeResult
{
    public required IPAddress DeviceAddress { get; set; }
    public required int[] Ports { get; set; }
    public string? DisplayName { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelName { get; set; }
    public string? ModelDescription { get; set; }
}

public class SsdpProbe
{
    public static async Task<List<SsdpProbeResult>> DiscoverDevices(IPAddress hostAddress, CancellationToken token = default)
    {
        List<SsdpProbeResult> ssdpDevices = [];

        using var deviceLocator = new SsdpDeviceLocator(hostAddress.ToString());
        IEnumerable<DiscoveredSsdpDevice> devices = await deviceLocator
            .SearchAsync(searchWaitTime: TimeSpan.FromSeconds(10), cancellationToken: token);

        foreach (DiscoveredSsdpDevice device in devices)
        {
            SsdpDevice? ssdpDevice = await RetryPipeline
                .ExecuteAsync(async token => await device.GetDeviceInfo(), cancellationToken: token);

            if (ssdpDevice == null) continue;

            foreach (var innerDevice in ssdpDevice.Devices)
            {
                if (innerDevice.PresentationUrl != null)
                {
                    SsdpProbeResult? parsedDevice = await ParseDevice(innerDevice, device, token);
                    if (parsedDevice != null)
                        ssdpDevices.Add(parsedDevice);
                }
            }

            if (ssdpDevice.PresentationUrl != null)
            {
                SsdpProbeResult? parsedDevice = await ParseDevice(ssdpDevice, device, token);
                if (parsedDevice != null)
                    ssdpDevices.Add(parsedDevice);   
            }
        }

        return [.. ssdpDevices.DistinctBy(device => device.DeviceAddress)];
    }

    public static async Task<SsdpProbeResult?> ParseDevice(SsdpDevice ssdpDevice, DiscoveredSsdpDevice discoveredDevice, CancellationToken token = default)
    {
        IPAddress? deviceAddress = await ResolveHost(ssdpDevice.PresentationUrl!.Host, token);

        if (deviceAddress == null)
            return null;

        List<int> ports = [];

        if (ssdpDevice.PresentationUrl!.Port > 0)
            ports.Add(ssdpDevice.PresentationUrl.Port);

        if (discoveredDevice.DescriptionLocation?.Port > 0)
            ports.Add(discoveredDevice.DescriptionLocation.Port);

        if (ssdpDevice.Services != null)
            ports.AddRange(ssdpDevice.Services
                .Where(service => service.ControlUrl != null
                    && service.ControlUrl.IsAbsoluteUri
                    && service.ControlUrl.Port > 0)
                .Select(service => service.ControlUrl!.Port));

        return new SsdpProbeResult
        {
            DeviceAddress = deviceAddress,
            Ports = [.. ports.Distinct()],
            DisplayName = ssdpDevice.FriendlyName,
            Manufacturer = ssdpDevice.Manufacturer,
            ModelName = string.IsNullOrEmpty(ssdpDevice.ModelName)
                ? (string.IsNullOrEmpty(ssdpDevice.ModelNumber) ? null : ssdpDevice.ModelNumber)
                : ssdpDevice.ModelName,
            ModelDescription = ssdpDevice.ModelDescription
        };
    }

    private static async Task<IPAddress?> ResolveHost(string hostname, CancellationToken token = default)
    {
        if (IPAddress.TryParse(hostname, out IPAddress? address))
            return address;
        
        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostname, token);
            return addresses
                .FirstOrDefault(address =>
                     address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private readonly static ResiliencePipeline<SsdpDevice?> RetryPipeline = 
        new ResiliencePipelineBuilder<SsdpDevice?>()
            .AddRetry(new RetryStrategyOptions<SsdpDevice?>()
            {
                ShouldHandle = new PredicateBuilder<SsdpDevice?>()
                    .Handle<Exception>()
                    .HandleResult(result => result == null),

                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Linear,
            })
            .AddFallback(new FallbackStrategyOptions<SsdpDevice?>()
            {
                ShouldHandle = new PredicateBuilder<SsdpDevice?>()
                    .Handle<Exception>()
                    .HandleResult(result => result == null),
                FallbackAction = _ => Outcome.FromResultAsValueTask<SsdpDevice?>(null)
            })
            .Build();
}