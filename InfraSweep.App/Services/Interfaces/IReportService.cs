using InfraSweep.App.Models;

namespace InfraSweep.App.Services.Interfaces;

public interface IReportService
{
    void SaveReport(ScanResult result, string filePath);
}