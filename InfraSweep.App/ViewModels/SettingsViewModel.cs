using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using InfraSweep.App.Helpers;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;

namespace InfraSweep.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private IScanScheduleService _scanScheduleService;

    public AppSettings AppSettings {get;}
    public bool IsWeeklySchedule => AppSettings.ScheduleFrequency == ScheduleFrequency.Weekly;
    public IEnumerable<DayOfWeek> AvailableDayOfWeek {get;} = 
        Enum.GetValues<DayOfWeek>();
    public IEnumerable<ScheduleFrequency> AvailableScheduleFrequency {get;} = 
        Enum.GetValues<ScheduleFrequency>();

    public SettingsViewModel(AppSettings settings, IScanScheduleService scanScheduleService, IStorageService storageService)
    {
        AppSettings = settings;
        AppSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.ScheduleFrequency) ||
                e.PropertyName == nameof(AppSettings.ScheduledDayOfWeek) ||
                e.PropertyName == nameof(AppSettings.ScheduledTime))
            {
                OnPropertyChanged(nameof(IsWeeklySchedule));
                _scanScheduleService?.ScheduleNextScan();
            }

            storageService.SaveAppSettings(AppSettings);
        };

        _scanScheduleService = scanScheduleService;
    }

    [RelayCommand]
    public void AutoStartToggle()
    {
        AutoStartHelper.Set(AppSettings.IsAutoStartEnabled);
    }

    [RelayCommand]
    public void ScanSchedulerToggle()
    {
        _scanScheduleService.ScheduleNextScan();
    }
}
