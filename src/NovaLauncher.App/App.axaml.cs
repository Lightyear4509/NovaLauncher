using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NovaLauncher.Application;
using NovaLauncher.Application.Themes;

namespace NovaLauncher.App;

public sealed partial class App : Avalonia.Application
{
    private static readonly Action<ILogger, Exception?> LogThemeInitializationFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(3, "ThemeInitializationFailure"),
        "Theme initialization failed; Nova Dark remains active");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Do not synchronously wait for settings I/O on Avalonia's UI thread.
            // ThemeService marshals palette application back to this dispatcher.
            Program.Services.GetRequiredService<IThemeHost>().Apply("nova-dark");
            desktop.MainWindow = new MainWindow
            {
                DataContext = Program.Services.GetRequiredService<MainWindowViewModel>(),
            };
            Dispatcher.UIThread.Post(InitializeThemeAsync, DispatcherPriority.Loaded);

            if (desktop.Args?.Contains("--smoke-test", StringComparer.Ordinal) == true)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    desktop.Shutdown(0);
                };
                timer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void InitializeThemeAsync()
    {
        try
        {
            await Program.Services.GetRequiredService<IThemeService>()
                .InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            var logger = Program.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
            LogThemeInitializationFailure(logger, exception);
        }
    }
}
