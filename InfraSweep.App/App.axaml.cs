using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using InfraSweep.App.ViewModels;
using InfraSweep.App.Views;
using InfraSweep.App.Services;
using System.Globalization;
using Avalonia.Controls;

namespace InfraSweep.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LocalizationService locale = new LocalizationService();
        string culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        try
        {
            locale.LoadCulture(culture);
        }
        catch
        {
            locale.LoadCulture("en");
        }

        ViewModelBase.Locale = locale;

        var dataContext = new MainWindowViewModel();

        var saved = StorageService.LoadScanResult();
        
        if (saved != null && saved.Hosts.Count > 0)
        {
            dataContext.ScanResultsView.LatestResult = saved;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = dataContext,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}