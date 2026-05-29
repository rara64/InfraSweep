using System.Collections.Generic;
using InfraSweep.Analysis;
using System.Linq;

public class ScanResult
{
    public List<HostScanResult> Hosts {get; set; } = new();
}

public class HostScanResult
{
    public required AnalyzedHost Host {get; set;}
    public VulnerabilityPage? HostVulnerabilities {get; set;}
    public List<SoftwareResult>? FoundSoftware {get; set;} = [];
    public int TotalCveCount => (HostVulnerabilities?.Cves.Count ?? 0)
        + (FoundSoftware?.Sum(s => s.Vulnerabilities?.Cves.Count ?? 0) ?? 0);
}

public class SoftwareResult
{
    public required IdentifiedSoftware Software {get; set;}
    public string Cpe {get; set;} = "";
    public VulnerabilityPage? Vulnerabilities {get; set;}
    public int Port {get; set;} = -1;
}