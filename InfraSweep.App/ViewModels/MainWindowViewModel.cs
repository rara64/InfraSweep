using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;
using System.Linq;
using InfraSweep.Discovery.Exceptions;
using InfraSweep.Analysis.Exceptions;
using System.Net.Http;
using InfraSweep.App.Helpers;
using System.Net.Sockets;

namespace InfraSweep.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private CancellationTokenSource _cts = new();
    private IScanService _scanService;
    private IStorageService _storageService;

    [ObservableProperty]
    private string _scanStatusText = ResourceHelper.GetString("ScanProgressProcessing");

    [ObservableProperty]
    private string _lastScanTimeString = "";

    [ObservableProperty]
    private int _scanProgress = 0;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private int _selectedTab = 0;

    [ObservableProperty]
    private string _lastScanError = "";

    [ObservableProperty]
    private bool _isCancelButtonEnabled = true;

    public bool IsSilentStart {get; set;} = false;
    public ObservableCollection<HostScanResult> HostScanResults { get; } = [];
    public ScanResultsViewModel ScanResultsView { get; }
    public SettingsViewModel SettingsView { get; }

    public MainWindowViewModel(
        AppSettings settings, 
        IScanScheduleService scanScheduleService, 
        IReportService reportService, 
        IScanService scanService,
        IStorageService storageService)
    {
        SettingsView = new SettingsViewModel(settings, scanScheduleService, storageService);
        ScanResultsView = new ScanResultsViewModel(reportService);
        _scanService = scanService;
        _storageService = storageService;
        scanScheduleService.ScanTriggered += async (_,_) => { await ScanNow(); };
    }

    [RelayCommand]
    public async Task ScanNow()
    {
        if (IsScanning)
            return;
        
        IsScanning = true;
        SelectedTab = 0;
        ScanProgress = 0;
        IsCancelButtonEnabled = true;
        HostScanResults.Clear();

        try
        {
            _cts = new CancellationTokenSource();

            var progress = new Progress<int>(value => {
                ScanProgress = value;
                ScanStatusText = value switch { 
                    < 33 => ResourceHelper.GetString("ScanProgressDiscoveringDevices"), 
                    < 66 => ResourceHelper.GetString("ScanProgressAnalyzingResults"),
                    < 100 => ResourceHelper.GetString("ScanProgressFindingVulnerabilities"),
                    _ => ResourceHelper.GetString("ScanProgressProcessing")
                };
            });

            ScanResult result = await Task.Run(() => _scanService.RunAsync(progress, _cts.Token));

            var previousResult = ScanResultsView.LatestResult;
            var cveOnlyNotifications = SettingsView.AppSettings.CveOnlyNotifications;

            await Task.Run(() =>
            {
                if (previousResult != null)
                {
                    bool different = ScanCompareHelper.CompareScans(previousResult, result);

                    if (different)
                    {
                        bool hasNewCves = result.Networks.Any(network => network.Hosts.Any(host => host.HasNewCves));

                        if (cveOnlyNotifications && hasNewCves)
                            _= NativeNotificationHelper.Send(ResourceHelper.GetString("NewCvesFoundTitle"), 
                                ResourceHelper.GetString("NewCvesFoundDescription"));
                        else
                            _= NativeNotificationHelper.Send(ResourceHelper.GetString("NewHostsOrCvesFoundTitle"), 
                                ResourceHelper.GetString("NewHostsOrCvesFoundDescription"));
                    }
                }

                _storageService.SaveScanResult(result);
            });

            ScanResultsView.LatestResult = result;

            LastScanTimeString = DateTime.Now.ToString("g");

            SelectedTab = 1;
        }
        catch (NoIpV4NetworkException)
        {
            LastScanError = ResourceHelper.GetString("OnlyIPv4NetworksSupported");
        }
        catch (HttpRequestException e) when (e.InnerException is CertPinException)
        {
            LastScanError = ResourceHelper.GetString("CertPinCheckFailed");
        }
        catch (HttpRequestException)
        {
            LastScanError = ResourceHelper.GetString("DataProviderDown");
        }
        catch (OperationCanceledException)
        {
            LastScanError = ResourceHelper.GetString("ScanCancelledByUser");
        }
        catch (Exception e)
        {
            LastScanError = ResourceHelper.GetString("ScanError").Replace("@", e.GetType().Name);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        IsCancelButtonEnabled = false;
        _cts.Cancel();
    }
}
