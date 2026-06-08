using InfraSweep.Discovery;
using System.Text.RegularExpressions;
using InfraSweep.Analysis.Models;

namespace InfraSweep.Analysis;

public class AnalyzedHost
{
    public required string Address {get; set;}
    public required string MacAddress { get; set; }
    public string? HostCpe {get; set;}
    public List<AnalyzedServiceInfo> Services {get; set;} = [];
    public string? FriendlyName {get; set;}
}

public class AnalyzedServiceInfo
{
    public int Port {get; set;}
    public string Banner {get; set;} = "";
    public List<IdentifiedSoftware> IdentifiedSoftware {get; set;} = [];

}

public class IdentifiedSoftware
{
    public required string Name {get; set;}
    public string? Version {get; set;}
    public List<string>? MatchedCpes {get; set;}
}

public class NetworkAnalysisResult
{
    public required string DisplayName {get; set;}
    public List<AnalyzedHost> Hosts {get; set;} = [];
}

public class NetworkAnalysis
{
    public static async Task<List<NetworkAnalysisResult>> AnalyzeDiscoveredNetworks(List<NetworkDiscoveryResult> scanResults, Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        List<NetworkAnalysisResult> results = [];

        int totalHosts = scanResults.Sum(network => network.Hosts.Count);

        if (scanResults.Count == 0 || totalHosts == 0)
            progressCallback?.Invoke(100);

        int currentItem = 0;

        foreach (NetworkDiscoveryResult network in scanResults)
        {
            List<AnalyzedHost> hosts = [];

            foreach (DiscoveredHost result in network.Hosts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                string? hostCpe = await GetHostCpeFromDeviceDetails(result, cancellationToken);

                if (result.HostedServices == null)
                {
                    currentItem++;
                    progressCallback?.Invoke(currentItem * 100 / totalHosts);
                    continue;
                }

                List<AnalyzedServiceInfo> identifiedServices = 
                    await AnalyzeServiceInfos(result.HostedServices, cancellationToken);

                currentItem++;
                progressCallback?.Invoke(currentItem * 100 / totalHosts);

                hosts.Add(new AnalyzedHost()
                {
                    Address = result.Address,
                    MacAddress = result.MacAddress,
                    HostCpe = hostCpe,
                    Services = identifiedServices,
                    FriendlyName = !string.IsNullOrEmpty(result.DisplayName) ? result.DisplayName :
                        !string.IsNullOrEmpty(result.ModelName) ? result.ModelName :
                        !string.IsNullOrEmpty(result.ModelDescription) ? result.ModelDescription :
                        !string.IsNullOrEmpty(result.Manufacturer) ? result.Manufacturer :
                        null,
                });
            }

            results.Add(new NetworkAnalysisResult()
            {
                DisplayName = network.DisplayName,
                Hosts = hosts
            });
        }

        return results;
    }

    private static readonly List<string> GenericKeywords = ["ssh", "upnp", "sdk"];
    private static bool IsGenericName(string input) =>
        GenericKeywords.Any(keyword => keyword.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase));

    private static List<IdentifiedSoftware> GetSoftwareFromBanner(string banner)
    {
        List<IdentifiedSoftware> identifiedSoftware = [];

        if (Common.HasPairSeparatorsRegex().IsMatch(banner))
        {
            foreach (Match match in Common.SoftwareVersionPairRegex().Matches(banner))
            {
                if (!IsGenericName(match.Groups["software"].Value))
                    identifiedSoftware.Add(new IdentifiedSoftware()
                    {
                        Name = match.Groups["software"].Value,
                        Version = match.Groups["version"].Value
                    });
            }

            if (identifiedSoftware.Count > 0)
                return identifiedSoftware;
        }

        if (Common.SoftwareNameRegex().IsMatch(banner.Trim())) {
            identifiedSoftware.Add(new IdentifiedSoftware()
            {
                Name = banner.Trim(),
                Version = null
            });

            return identifiedSoftware;
        }

        string cleanedBanner = banner
            .Replace("-"," ")
            .Replace("_"," ");
            
        foreach (Match match in Common.SoftwareVersionPairRegex().Matches(cleanedBanner))
        {
            if (!IsGenericName(match.Groups["software"].Value))
                identifiedSoftware.Add(new IdentifiedSoftware()
                {
                    Name = match.Groups["software"].Value,
                    Version = match.Groups["version"].Value
                });
        }

        return identifiedSoftware;
    }

    private static async Task<string?> GetHostCpeFromDeviceDetails(DiscoveredHost result, CancellationToken cancellationToken = default)
    {
        if (result.ModelName != null || result.ModelDescription != null)
        {
            List<string?[]> queries =
            [
                [result.Manufacturer, result.ModelName ?? result.ModelDescription],
                [result.ModelName ?? result.ModelDescription]
            ];

            foreach (var query in queries)
            {
                string? cpe = await CpeGuesser.TryGetMatchingCpeFromStrings(
                    [.. query
                    .Where(term => term != null)
                    .Select(term => term!.TrimStart())], cancellationToken);
                
                if (cpe != null)
                    return cpe;
            }
        }
        else if (result.DisplayName != null)
        {
            string? cpe = await CpeGuesser.TryGetMatchingCpeFromTokens(result.DisplayName, cancellationToken);

            if (cpe != null)
                return cpe;
        }

        return null;
    }

    private static async Task<List<AnalyzedServiceInfo>> AnalyzeServiceInfos(List<ServiceInfo> serviceInfos, CancellationToken cancellationToken = default)
    {
        var result = new List<AnalyzedServiceInfo>();

        foreach (ServiceInfo info in serviceInfos)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (info.Banner == null)
            {
                Common.IanaPortAssignments.TryGetValue(info.Port, out var name);
                result.Add(new AnalyzedServiceInfo {
                    IdentifiedSoftware = [
                        new IdentifiedSoftware(){
                            Name = name ?? ""
                        }
                    ],
                    Port = info.Port 
                });
                continue;
            }

            List<IdentifiedSoftware> identifiedSoftware = GetSoftwareFromBanner(info.Banner);

            foreach (IdentifiedSoftware software in identifiedSoftware)
            {
                List<string> matchedCpe = await ResolveCpesForSoftware(software, cancellationToken);
                software.MatchedCpes = [.. matchedCpe];
            }

            result.Add(new AnalyzedServiceInfo
            {
                Port = info.Port,
                Banner = info.Banner,
                IdentifiedSoftware = identifiedSoftware
            });
        }
        
        return result;
    }

    private static async Task<List<string>> ResolveCpesForSoftware(IdentifiedSoftware software, CancellationToken cancellationToken = default)
    {
        if (Common.KnownCpe.TryGetValue(software.Name.ToLower(), out string? knownCpe))
            return [knownCpe];

        string? cpe = await CpeGuesser.TryGetMatchingCpeFromTokens(software.Name, cancellationToken);

        List<string> matchedCpes = [];
        
        if (cpe != null)
        {
            matchedCpes.Add(cpe);

            CpeVulnerabilitySearchResult? searchResult = await Api.GetVulnerabilitiesByCpe(cpe, cancellationToken: cancellationToken);

            if (searchResult != null)
            {
                int? latestCveYear = searchResult.CveEntries
                    .Select(entry => entry.Metadata)
                    .DefaultIfEmpty()
                    .Max(metadata => metadata?.DatePublished?.Year);
                
                int currentYear = DateTime.UtcNow.Year;

                if (latestCveYear != null && currentYear - latestCveYear >= 10 )
                {
                    string? extraCpe = await GetCpeBasedOnProductSearch(software.Name, cancellationToken);

                    if (extraCpe != null)
                    {
                        matchedCpes.Add(extraCpe);
                        matchedCpes.Reverse();   
                    }
                }
            } 
        }

        return matchedCpes;
    }

    private static async Task<string?> GetCpeBasedOnProductSearch(string softwareName, CancellationToken cancellationToken = default)
    {
        string? firstProductName = await Api.GetFirstMatchedProductFromString(softwareName, cancellationToken);

        if (firstProductName != null)
        {
            string[] query = [softwareName, ..firstProductName.Split(" ")];

            return await CpeGuesser.TryGetMatchingCpeFromStrings(query, cancellationToken);
        }

        return null;
    }
}