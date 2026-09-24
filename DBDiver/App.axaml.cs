using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DBDiver.Views;

namespace DBDiver;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            Task.Run(async () =>
            {
                await Task.Delay(3000);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var mainWin = new MainWindow();
                    desktop.MainWindow = mainWin;
                    mainWin.Opened += (_, _) => splash.Close();
                    mainWin.Show();
                    mainWin.WindowState = WindowState.Maximized;
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
