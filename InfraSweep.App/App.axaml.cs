using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InfraSweep.App.ViewModels;
using InfraSweep.App.Views;
using System.Globalization;
using System;
using Avalonia.Markup.Xaml.Styling;
using InfraSweep.App.Models;
using InfraSweep.App.Services;
using Microsoft.Extensions.DependencyInjection;
using InfraSweep.App.Services.Interfaces;
using Avalonia.Controls;
using System.Threading;

namespace InfraSweep.App;

public partial class App : Application
{
    public IServiceProvider? Services {get; private set;}
    public static bool IsExplicitShutdown {get; set;} = false;
    private readonly SemaphoreSlim trayClickLock = new(1, 1);

    public override void Initialize()
    {
        string culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        ResourceInclude resourceInclude = new(new Uri("avares://InfraSweep.App/App.axaml"))
        {
            Source = culture switch
            {
                "pl" => new Uri($"avares://InfraSweep.App/Resources/StringResources.pl.xaml"),
                _ => new Uri($"avares://InfraSweep.App/Resources/StringResources.xaml"),
            }
        };

        Resources.MergedDictionaries.Add(resourceInclude);

        AvaloniaXamlLoader.Load(this);   
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton<IScanService, ScanService>();
        serviceCollection.AddSingleton<IScanScheduleService, ScanScheduleService>();
        serviceCollection.AddSingleton<IReportService, ReportService>();
        serviceCollection.AddSingleton<IStorageService, StorageService>();
        serviceCollection.AddSingleton(provider =>
        {
           var storage = provider.GetRequiredService<IStorageService>();
           return storage.LoadAppSettings() ?? new AppSettings();
        });
        serviceCollection.AddSingleton<MainWindowViewModel>();

        Services = serviceCollection.BuildServiceProvider();

        var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();

        LoadLastScan(mainViewModel);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            bool isSilent = desktop.Args?.Contains("-silent") ?? false;
            mainViewModel.IsSilentStart = isSilent;
            
            if (!isSilent)
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };
        }

        var activatable =
            Current?.TryGetFeature<IActivatableLifetime>();

        activatable?.Activated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Reopen)
                {
                    if (Current is App app)
                        app.HandleTrayIconClick(null, EventArgs.Empty);
                }
            }; 

        base.OnFrameworkInitializationCompleted();
    }

    private void LoadLastScan(MainWindowViewModel viewModel)
    {
        var storage = Services?.GetRequiredService<IStorageService>();
        var saved = storage?.LoadScanResult();
        
        if (saved != null && saved.Networks.Count > 0)
        {
            viewModel.ScanResultsView.LatestResult = saved;
            viewModel.LastScanTimeString = saved.TimeOfScan.ToString("g");
        }
    }

    public void HandleNativeMenuExitClick(object? sender, EventArgs args)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IsExplicitShutdown = true;
            desktop.Shutdown();
        }
    }

    public void HandleTrayIconClick(object? sender, EventArgs args)
    {
        if (!trayClickLock.Wait(0))
            return;
        
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow == null)
                {   
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = Services?.GetRequiredService<MainWindowViewModel>(),
                    };
                }

                var mainWindow = desktop.MainWindow;

                if (!mainWindow.IsVisible)
                    mainWindow.Show();

                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                mainWindow.Activate();
                mainWindow.Focus();
            }      
        }
        finally
        {
            trayClickLock.Release();
        }
    }
}