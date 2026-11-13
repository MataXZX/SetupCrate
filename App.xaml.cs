using System.IO;
using System.Windows;
using System.Windows.Threading;
using SetupCrate.Services;

namespace SetupCrate;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        var settings = SettingsStore.Load(AppContext.BaseDirectory);
        ThemeManager.Apply(ThemeManager.Parse(settings.Theme));
        Loc.Apply(settings.Language switch
        {
            "ru" => AppLang.Ru,
            "en" => AppLang.En,
            _ => Loc.Detect()
        });

        new MainWindow().Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {args.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }

        MessageBox.Show(args.Exception.ToString(), "SetupCrate",
            MessageBoxButton.OK, MessageBoxImage.Error);

        args.Handled = true;
    }
}
