using System.Linq;
using InfraSweep.App.Models;

namespace InfraSweep.App.Helpers;

public class ScanCompareHelper
{
    public static bool CompareScans(ScanResult previousResult, ScanResult newResult)
    {
        if (previousResult == null || newResult == null)
            return false;

        var prevHostAddresses = previousResult.Networks
            .SelectMany(n => n.Hosts)
            .Select(h => h.Host.MacAddress)
            .ToHashSet();
        
        var prevHostCves = previousResult.Networks
            .SelectMany(n => n.Hosts)
            .SelectMany(h => h.HostVulnerabilities?.Cves?
                .Select(c => (h.Host.MacAddress, c.CveId)) ?? [])
            .ToHashSet();

        var prevSoftCves = previousResult.Networks
            .SelectMany(n => n.Hosts)
            .SelectMany(h => (h.FoundSoftware ?? [])
                .SelectMany(s => s.Vulnerabilities?.Cves?
                    .Select(c => (h.Host.MacAddress, s.Software.Name, c.CveId)) ?? []))
            .ToHashSet();

        bool newScanHasChanges = false;

        foreach (var host in newResult.Networks.SelectMany(n => n.Hosts))
        {
            host.IsNewHost = !prevHostAddresses.Contains(host.Host.MacAddress);
            bool hasNewCves = false;

            if (host.IsNewHost)
            {
                newScanHasChanges = true;
                host.HasNewCves = false;
                continue;
            }

            foreach (var cve in host.HostVulnerabilities?.Cves ?? [])
                if (!prevHostCves.Contains((host.Host.MacAddress, cve.CveId)))
                {
                    newScanHasChanges = true;
                    cve.IsHighlighted = true;
                    hasNewCves = true;
                }

            foreach (var software in host.FoundSoftware ?? [])
                foreach (var cve in software.Vulnerabilities?.Cves ?? [])
                {
                    if (!prevSoftCves.Contains(
                        (host.Host.MacAddress, software.Software.Name, cve.CveId)))
                    {
                        newScanHasChanges = true;
                        cve.IsHighlighted = true;
                        hasNewCves = true;
                    }  
                }

            host.HasNewCves = hasNewCves;
        }

        return newScanHasChanges;
    }  
}