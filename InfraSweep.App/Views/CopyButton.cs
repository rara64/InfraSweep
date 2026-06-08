using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace InfraSweep.App.Views;

public class CopyButton : Button
{
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(CopyText));
    
    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    protected override async void OnClick()
    {
        base.OnClick();
        if (!string.IsNullOrEmpty(CopyText))
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is not null)
                await topLevel.Clipboard.SetTextAsync(CopyText);
        }
    }
}