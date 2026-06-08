using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using InfraSweep.App.Helpers;
using InfraSweep.App.Models;

namespace InfraSweep.App.Converters;

public class EnumToDisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ScheduleFrequency.Daily => ResourceHelper.GetString("EnumScheduleFrequencyDaily"),
            ScheduleFrequency.Weekly => ResourceHelper.GetString("EnumScheduleFrequencyWeekly"),
            DayOfWeek dayOfWeek => culture.DateTimeFormat.GetDayName(dayOfWeek),
            _ => value?.ToString()
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}