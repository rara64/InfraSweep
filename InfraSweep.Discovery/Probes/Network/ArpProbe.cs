using System.Net;
using System.Net.NetworkInformation;
using ArpLookup;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace InfraSweep.Discovery.Probes.Network;

public class ArpProbe
{
    public static async Task<PhysicalAddress?> GetPhysicalAddress
        (IPAddress address)
    {
        PhysicalAddress? macAddress;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            macAddress = await MacOSLookup(address);
        else
            macAddress = await Arp.LookupAsync(address);

        if (macAddress?.GetAddressBytes()?.All(b => b.Equals(0x0)) ?? false)
            return null;

        return macAddress;
    }

    private static async Task<PhysicalAddress?> MacOSLookup(IPAddress address)
    {
        using var ping = new Ping();
        await ping.SendPingAsync(address, timeout: 2000);

        using Process arpProcess = new()
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

        var match = Regex.Match(arpOutput, 
            $@"\({Regex.Escape(address.ToString())}\)\s+at\s+(?<mac>[0-9a-f:]+)");

        if (!match.Success)
            return null;

        return new PhysicalAddress(
            [.. match.Groups["mac"].Value
                .Split(":")
                .Select(part => Convert.ToByte(part, 16))
            ]
        );
    }
}