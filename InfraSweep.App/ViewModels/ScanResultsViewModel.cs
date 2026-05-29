using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InfraSweep.Analysis;

namespace InfraSweep.App.ViewModels;

public partial class ScanResultsViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    [NotifyPropertyChangedFor(nameof(HostCount))]
    [NotifyPropertyChangedFor(nameof(ServiceCount))]
    [NotifyPropertyChangedFor(nameof(VulnerabilityCount))]
    [NotifyPropertyChangedFor(nameof(CurrentYearVulnerabilityCount))]
    private ScanResult? _latestResult;

    public bool HasResults => LatestResult != null && LatestResult.Hosts.Count > 0;
    public int HostCount => (LatestResult != null) ? LatestResult.Hosts.Count : 0;
    public int ServiceCount => (LatestResult != null) ? LatestResult.Hosts.Sum(host => host.FoundSoftware?.Count ?? 0) : 0;

    public int VulnerabilityCount => (LatestResult != null) ? LatestResult.Hosts
        .Sum(host => (host.HostVulnerabilities?.Cves.Count ?? 0) + (host.FoundSoftware?
            .Sum(soft => (soft.Vulnerabilities?.Cves.Count) ?? 0) ?? 0))
        : 0;

    public int CurrentYearVulnerabilityCount => (LatestResult != null) ? LatestResult.Hosts
        .Sum(host => 
            host.HostVulnerabilities?.Cves
                .Count(cve => cve.DatePublished?.Year == DateTimeOffset.UtcNow.Year) ?? 0
            + host.FoundSoftware?.Sum(soft => soft.Vulnerabilities?.Cves?
                .Count(cve => cve.DatePublished?.Year == DateTimeOffset.UtcNow.Year)) ?? 0)
        : 0;
}
