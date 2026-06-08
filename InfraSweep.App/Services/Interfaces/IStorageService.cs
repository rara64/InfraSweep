using InfraSweep.App.Models;

namespace InfraSweep.App.Services.Interfaces;

public interface IStorageService
{
    public void SaveScanResult(ScanResult result);
    public ScanResult? LoadScanResult();

    public AppSettings? LoadAppSettings();
    public void SaveAppSettings(AppSettings settings);
}