using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfraSweep.Analysis;
using InfraSweep.Discovery;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;

namespace InfraSweep.App.Services;

public class ScanService : IScanService
{
    private AppSettings _appSettings;

    public ScanService(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<ScanResult> RunAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var scanResult = new ScanResult
        {
            TimeOfScan = DateTimeOffset.UtcNow
        };
        
        List<NetworkDiscoveryResult> discoveredNetworks = await NetworkScanner.DiscoverNetworks((value) =>
        {
            progress?.Report(value * 33 / 100);

        }, cancellationToken);

        List<NetworkAnalysisResult> analyzedNetworks = await NetworkAnalysis.AnalyzeDiscoveredNetworks(discoveredNetworks, (value) =>
        {
            progress?.Report(33 + (value * 33 / 100));

        }, cancellationToken);

        scanResult.Networks.AddRange(await BuildScanResultsForNetworks(analyzedNetworks, (value) =>
        {
            progress?.Report(66 + (value * 34 / 100));

        }, cancellationToken));

        scanResult.Metadata = GenerateMetadata(scanResult);

        progress?.Report(100);

        return scanResult;
    }

    private async Task<List<NetworkScanResult>> BuildScanResultsForNetworks(
        List<NetworkAnalysisResult> networks, 
        Action<int>? progressCallback = null, 
        CancellationToken cancellationToken = default)
    {
        List<NetworkScanResult> results = [];

        int currentHost = 0;
        int totalHostCount = networks.Sum(n => n.Hosts.Count);

        if (totalHostCount == 0)
        {
            progressCallback?.Invoke(100);
        }

        List<NetworkScanResult> networkScanResults = [];

        foreach (NetworkAnalysisResult network in networks)
        {
            List<HostScanResult> hosts = [];

            foreach (AnalyzedHost host in network.Hosts)
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
                        if (_appSettings.ShowCveOnlyForKnownVersions && string.IsNullOrEmpty(soft.Version))
                        {
                            hostResult?.FoundSoftware?.Add(new SoftwareResult
                            {
                                Software = soft,
                                Port = info.Port
                            });
                            continue;
                        }
                        
                        if (soft.MatchedCpes != null && soft.MatchedCpes.Count > 0)
                        {
                            VulnerabilityQueryResult? queryResult = new(){ Page = 1 };
                            if (soft.MatchedCpes.Count > 1)
                            {
                                bool hasNextPage = false;

                                foreach(var cpe in soft.MatchedCpes)
                                {
                                    if (string.IsNullOrWhiteSpace(cpe))
                                        continue;
                                
                                    VulnerabilityQueryResult? page = await VulnerabilityResolver.GetVulnerabilities(cpe, soft.Version);
                                
                                    if (page != null)
                                    {
                                        queryResult.Cves.AddRange(page.Cves);
                                        if (!hasNextPage)
                                            hasNextPage = page.NextPageExists;
                                    }
                                }

                                queryResult.NextPageExists = hasNextPage;   
                            }
                            else
                            {
                                queryResult = await VulnerabilityResolver.GetVulnerabilities(soft.MatchedCpes.First(), soft.Version);
                            }

                            if (queryResult != null)
                            {
                                hostResult?.FoundSoftware?.Add(new SoftwareResult
                                {
                                    Software = soft,
                                    Cpe = string.Join("|", soft.MatchedCpes),
                                    Vulnerabilities = queryResult,
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

                hostResult?.FoundSoftware = hostResult?.FoundSoftware?
                    .OrderByDescending(soft => soft.Vulnerabilities?.Cves?.Count ?? 0)
                    .ToList();

                if (hostResult != null)
                    hosts.Add(hostResult);
                
                currentHost++;
                progressCallback?.Invoke(currentHost * 100 / totalHostCount);
            }

            hosts = [.. hosts.OrderByDescending(host => host.TotalCveCount)];

            results.Add(new NetworkScanResult()
            {
                DisplayName = network.DisplayName,
                Hosts = hosts
            });
        }

        return results;
    }

    private ScanMetadata GenerateMetadata(ScanResult result)
    {
        return new ScanMetadata()
        {
            HasResults = result.Networks
                .Sum(net => net.Hosts.Count) > 0,

            HostCount = result.Networks
                .Sum(net => net.Hosts.Count),

            ServiceCount = result.Networks
                .SelectMany(net => net.Hosts)
                .Sum(hosts => hosts.FoundSoftware?.Count ?? 0),

            VulnerabilityCount = result.Networks
                .SelectMany(net => net.Hosts)
                .Sum(host => 
                    (host.HostVulnerabilities?.Cves.Count ?? 0) +
                    (host.FoundSoftware?.Sum(soft => soft.Vulnerabilities?.Cves?.Count ?? 0) ?? 0)),

            CurrentYearVulnerabilityCount = result.Networks
                .SelectMany(net => net.Hosts)
                .Sum(host => 
                    (host.HostVulnerabilities?.Cves
                        .Count(cve => cve.DatePublished?.Year == DateTimeOffset.UtcNow.Year) ?? 0)
                    + (host.FoundSoftware?.Sum(soft => soft.Vulnerabilities?.Cves?
                        .Count(cve => cve.DatePublished?.Year == DateTimeOffset.UtcNow.Year) ?? 0) ?? 0)),

            HasHighlighted = result.Networks
                .SelectMany(net => net.Hosts)
                .Any(host => host.IsNewHost || host.HasNewCves)
        };
    }
}