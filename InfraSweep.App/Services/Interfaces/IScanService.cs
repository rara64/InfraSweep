using System;
using System.Threading;
using System.Threading.Tasks;
using InfraSweep.App.Models;

namespace InfraSweep.App.Services.Interfaces;

public interface IScanService
{
    Task<ScanResult> RunAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}