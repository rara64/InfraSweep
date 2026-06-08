using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfraSweep.App.Helpers;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;
using InfraSweep.App.Views;

namespace InfraSweep.App.ViewModels;

public partial class ScanResultsViewModel : ViewModelBase
{
    private IReportService _reportService;
    public ScanResultsViewModel(IReportService reportService)
    {
        _reportService = reportService;
    }

    [ObservableProperty]
    public ScanResult? _latestResult;

    [RelayCommand]
    public async Task GenerateReport()
    {
        if (LatestResult == null)
            return;

        if (Avalonia.Application.Current?.ApplicationLifetime 
            is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow == null)
            return;

        var mainWindow = desktop.MainWindow;

        try
        {
            var options = new FilePickerSaveOptions
            {
                Title = ResourceHelper.GetString("PdfFileDialogTitle"),
                DefaultExtension = "pdf",
                SuggestedFileName = "InfraSweep",
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF (*.pdf)")
                    {
                        Patterns = ["*.pdf"],
                        MimeTypes = ["application/pdf"]
                    }
                ]
            };

            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(options);
            
            if (file != null)
            {
                string filePath = file.Path.LocalPath;
                await Task.Run(() => _reportService.SaveReport(LatestResult, filePath));

                if (mainWindow is MainWindow window)
                    window.NotificationManager.Show(new Notification(
                        ResourceHelper.GetString("PdfFileSavedToastTitle"),
                        ResourceHelper.GetString("PdfFileSavedToastDescription"),
                        NotificationType.Success,
                        TimeSpan.FromSeconds(4)));
            }  
        }
        catch (Exception e)
        {
            if (mainWindow is MainWindow window)
                window.NotificationManager.Show(new Notification(
                    ResourceHelper.GetString("PdfFileSavedToastErrorTitle"),
                    ResourceHelper.GetString("PdfFileSavedToastErrorDescription").Replace("@",e.GetType().Name)),
                    NotificationType.Error,
                    TimeSpan.FromSeconds(4));
        }
    }
}
