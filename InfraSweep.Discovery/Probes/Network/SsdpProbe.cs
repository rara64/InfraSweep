using System.Net;
using Rssdp;

namespace InfraSweep.Discovery.Probes;

public class SsdpProbe
{
    public static async Task<List<SsdpDevice>> DiscoverDevices(IPAddress hostAddress)
    {
        List<SsdpDevice> ssdpDevices = [];

        using (SsdpDeviceLocator deviceLocator = new SsdpDeviceLocator(hostAddress.ToString()))
        {
            IEnumerable<DiscoveredSsdpDevice> devices = await deviceLocator.SearchAsync();
            foreach (DiscoveredSsdpDevice device in devices)
            {
                SsdpDevice ssdpDevice = await device.GetDeviceInfo();
                ssdpDevices.Add(ssdpDevice);
            }
        }

        return ssdpDevices;
    }
}