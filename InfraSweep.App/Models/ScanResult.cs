using System;
using System.Collections.Generic;
using System.Linq;
using InfraSweep.Analysis;

namespace InfraSweep.App.Models;

public class ScanResult
{
    public List<NetworkScanResult> Networks {get; set; } = [];
    public ScanMetadata Metadata {get; set;} = new();
    public DateTimeOffset TimeOfScan {get; set;} = new();
}

public class NetworkScanResult
{
    public required string DisplayName {get; set;}
    public List<HostScanResult> Hosts {get; set;} = [];
}

public class HostScanResult
{
    public required AnalyzedHost Host {get; set;}
    public VulnerabilityQueryResult? HostVulnerabilities {get; set;}
    public List<SoftwareResult>? FoundSoftware {get; set;} = [];
    public int TotalCveCount => (HostVulnerabilities?.Cves.Count ?? 0)
        + (FoundSoftware?.Sum(s => s.Vulnerabilities?.Cves.Count ?? 0) ?? 0);
    public bool HasNewCves {get; set;} = false;
    public bool IsNewHost {get; set;} = false;
}

public class SoftwareResult
{
    public required IdentifiedSoftware Software {get; set;}
    public string Cpe {get; set;} = "";
    public VulnerabilityQueryResult? Vulnerabilities {get; set;}
    public int Port {get; set;} = -1;
}

public class ScanMetadata
{
    public bool HasResults {get; set;} = false;
    public int HostCount {get; set;} = 0;
    public int ServiceCount {get; set;} = 0;
    public int VulnerabilityCount {get; set;} = 0;
    public int CurrentYearVulnerabilityCount {get; set;} = 0;
    public bool HasHighlighted {get; set;} = false;
}