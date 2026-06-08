using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using InfraSweep.App.Helpers;
using InfraSweep.App.ViewModels;

namespace InfraSweep.App.Views;

public partial class MainWindow : Window
{
    public WindowNotificationManager NotificationManager {get; set;}
    public MainWindow()
    {
        InitializeComponent();
        NotificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomCenter,
            MaxItems = 4
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (App.IsExplicitShutdown)
            return;

        var settings = (DataContext as MainWindowViewModel)?.SettingsView.AppSettings;
        var isSilent = (DataContext as MainWindowViewModel)?.IsSilentStart ?? false;
        
        if (settings?.IsHideToTrayEnabled == true || isSilent)
        {
            e.Cancel = true;

            Hide();

            Task.Run(() => NativeNotificationHelper.Send(
                ResourceHelper.GetString("WindowCloseNotificationTitle"),
                ResourceHelper.GetString("WindowCloseNotificationDescription")));
        }

        base.OnClosing(e);
    }
}