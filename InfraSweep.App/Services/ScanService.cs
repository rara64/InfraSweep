using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfraSweep.Analysis;
using InfraSweep.Discovery;

namespace InfraSweep.App.Services;

public class ScanService
{
    public static async Task<ScanResult> RunAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var scanResult = new ScanResult();

        List<AnalyzedHost> analyzedHosts = [];
        
        try
        {
            List<DiscoveredHost> discoveredHosts = await NetworkScanner.DiscoverHosts(cancellationToken, (value) =>
            {
                progress?.Report(value / 3);
            });

            analyzedHosts.AddRange(
            await NetworkAnalysis.AnalyzeDiscoveredHosts(discoveredHosts, cancellationToken, (value) =>
            {
                progress?.Report(33 + (value / 3));
            }));
        }
        catch
        {
            return new ScanResult();
        }

        int currentHost = 0;
        int totalHosts = analyzedHosts.Count;

        foreach (AnalyzedHost host in analyzedHosts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            HostScanResult hostResult = new() { Host = host };

            if (!string.IsNullOrEmpty(host.HostCpe))
            {
                hostResult.HostVulnerabilities = await VulnerabilityResolver.GetVulnerabilities(host.HostCpe);  
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
                        
                            if (softPage != null)
                            {
                                hostResult?.FoundSoftware?.Add(new SoftwareResult
                                {
                                    Software = soft,
                                    Cpe = cpe,
                                    Vulnerabilities = softPage,
                                    Port = info.Port
                                });
                            }
                        }
                    else
                    {
                        hostResult?.FoundSoftware?.Add(new SoftwareResult
                        {
                            Software = soft,
                            Port = info.Port
                        });
                    }
                }
            }

            scanResult.Hosts.Add(hostResult);
            currentHost++;

            progress?.Report(66 + ((currentHost * 100) / totalHosts) / 3);
        }

        scanResult.Hosts = scanResult.Hosts
            .OrderByDescending(host => host.TotalCveCount)
            .ToList();

        StorageService.SaveScanResult(scanResult);

        progress?.Report(100);
        return scanResult;
    }
}