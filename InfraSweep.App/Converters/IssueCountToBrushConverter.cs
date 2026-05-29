using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InfraSweep.App.Converters;

public class IssueCountToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int count)
            return new SolidColorBrush(Colors.Gray);

        return count switch
        {
            0 => new SolidColorBrush(Color.Parse("#43A047")),
            _ => new SolidColorBrush(Color.Parse("#D32F2F"))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}