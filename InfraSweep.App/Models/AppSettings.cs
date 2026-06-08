using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InfraSweep.App.Models;

public partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    public bool _isAutoStartEnabled = false;

    [ObservableProperty]
    public bool _isHideToTrayEnabled = false;

    [ObservableProperty]
    public bool _isScanSchedulerEnabled = false;

    [ObservableProperty]
    public ScheduleFrequency _scheduleFrequency = ScheduleFrequency.Weekly;

    [ObservableProperty]
    public TimeSpan _scheduledTime = new (19, 0, 0);

    [ObservableProperty]
    public DayOfWeek _scheduledDayOfWeek = DayOfWeek.Sunday;

    [ObservableProperty]
    public DateTimeOffset _lastScheduledScanTime = DateTimeOffset.MinValue;

    [ObservableProperty]
    public bool _cveOnlyNotifications = true;

    [ObservableProperty]
    public bool _showCveOnlyForKnownVersions = false;
}

public enum ScheduleFrequency
{
    Daily,
    Weekly
}