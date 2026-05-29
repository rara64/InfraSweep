using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InfraSweep.App.Converters;

public class SeverityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string severity)
            return new SolidColorBrush(Colors.Gray);

        return severity.ToUpperInvariant() switch
        {
            "CRITICAL" => new SolidColorBrush(Color.Parse("#D32F2F")),
            "HIGH" => new SolidColorBrush(Color.Parse("#E64A19")),
            "MEDIUM" => new SolidColorBrush(Color.Parse("#F9A825")),
            "LOW" => new SolidColorBrush(Color.Parse("#43A047")),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}