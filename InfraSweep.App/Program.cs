using Avalonia;
using InfraSweep.App.Helpers;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace InfraSweep.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    private const string PipeName = "InfraSweepInstanceLock";
    private static NamedPipeServerStream? _server;

    [STAThread]
    public static void Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        try
        {
            if (!OperatingSystem.IsMacOS())
            {
                if (TryAcquireFirstInstanceLock())
                    Task.Run(() => ListenForWakeSignal(cts.Token));
                else
                {
                    SignalFirstInstance();
                    return;
                }
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); 
        }
        finally
        {
            cts.Cancel();
        }
    }

    private static bool TryAcquireFirstInstanceLock()
    {
        try
        {
            _server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            return true;
        }
        catch { return false; }
    }

    private static void SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out);
            
            client.Connect(2000);
        }
        catch {}
    }

    private static async Task ListenForWakeSignal(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _server!.WaitForConnectionAsync(cancellationToken);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current is App app)
                        app.HandleTrayIconClick(null, EventArgs.Empty);
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                ShowInDock = false
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
