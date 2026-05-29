using CommunityToolkit.Mvvm.Input;
using InfraSweep.App.Services;
using InfraSweep.Discovery;
using InfraSweep.Analysis;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InfraSweep.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string ScanResultsTabLabel { get; } = "Scan Results";
    public string SettingsTabLabel {get; } = "Settings";

    [ObservableProperty]
    private string _lastScanTimeString = "";

    [ObservableProperty]
    private int _scanProgress = 0;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private int _selectedTab = 0;

    public ObservableCollection<HostScanResult> HostScanResults {get; } = new();

    public ScanResultsViewModel ScanResultsView {get; } = new();
    public SettingsViewModel SettingsView {get; } = new();

    [RelayCommand]
    public async Task ScanNow()
    {
        if (IsScanning)
            return;
        
        IsScanning = true;
        ScanProgress = 0;
        HostScanResults.Clear();

        try
        {
            var progress = new Progress<int>(value => ScanProgress = value);

            ScanResult result = await ScanService.RunAsync(progress);

            ScanResultsView.LatestResult = result;

            LastScanTimeString = DateTime.Now.ToString("g");

            SelectedTab = 1;
        }
        catch (OperationCanceledException) {}
        catch (Exception) {}
        finally
        {
            IsScanning = false;
        }
        
    }
}
