using System;

namespace InfraSweep.App.Services.Interfaces;

public interface IScanScheduleService
{
    void ScheduleNextScan();
    void StopTimer();
    event EventHandler? ScanTriggered;
}