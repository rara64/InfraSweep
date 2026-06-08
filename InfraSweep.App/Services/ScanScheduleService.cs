using System;
using System.Threading;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;

namespace InfraSweep.App.Services;

public class ScanScheduleService : IScanScheduleService, IDisposable
{
    private Timer? _timer;
    private readonly AppSettings _settings;

    public ScanScheduleService(AppSettings settings)
    {
        _settings = settings;
        ScheduleNextScan();
    }

    public event EventHandler? ScanTriggered;

    public void ScheduleNextScan()
    {
        StopTimer();

        if (_settings == null || _settings.IsScanSchedulerEnabled == false)
            return;

        DateTime now = DateTime.Now;

        DateTime nextRun = CalculateNextScanTime(
            _settings.LastScheduledScanTime, 
            _settings.ScheduledTime, 
            _settings.ScheduledDayOfWeek, 
            _settings.ScheduleFrequency,
            now);
        
        TimeSpan delay = nextRun - now;

        if (_timer == null)
            _timer = new Timer(TimerElapsedCallback, null, delay, Timeout.InfiniteTimeSpan);
        else
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    public void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private static DateTime CalculateNextScanTime(
        DateTimeOffset lastScan, 
        TimeSpan scheduledTime, 
        DayOfWeek scheduledDayOfWeek, 
        ScheduleFrequency scheduleFrequency,
        DateTime now)
    {
        DateTime baseDate = (lastScan == DateTimeOffset.MinValue)
            ? DateTime.Today
            : lastScan.ToLocalTime().Date;

        DateTime nextRun = baseDate.Add(scheduledTime);

        if (scheduleFrequency == ScheduleFrequency.Weekly)
        {
            int daysUntilTarget = ((int)scheduledDayOfWeek - (int)nextRun.DayOfWeek + 7) % 7;
            nextRun = nextRun.AddDays(daysUntilTarget);
        }

        if (nextRun <= now)
        {
            if (scheduleFrequency == ScheduleFrequency.Weekly)
                nextRun = nextRun.AddDays(7);
            else
                nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }

    private async void TimerElapsedCallback(object? state)
    {
        ScanTriggered?.Invoke(this, EventArgs.Empty);

        if (_settings != null)
        {
            _settings.LastScheduledScanTime = DateTimeOffset.Now;
        }

        ScheduleNextScan();
    }
}