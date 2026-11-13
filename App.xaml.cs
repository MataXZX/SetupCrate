using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        if (e.Args.Contains("--dump-loc", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var probe = new MainWindow();
            probe.Show();
            probe.UpdateLayout();

            var names = new[] { "BtnDownload", "BtnInstall", "BtnCheck", "BtnLogToggle", "BtnLang", "BtnToggleAll", "BtnTheme" };
            var dump = new List<string>();

            foreach (var lang in new[] { AppLang.Ru, AppLang.En })
            {
                Loc.Apply(lang);
                probe.UpdateLayout();
                probe.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                foreach (var name in names)
                {
                    if (probe.FindName(name) is System.Windows.Controls.Button button)
                        dump.Add($"{name}[{lang}]={(button.Content as string ?? "(null)")}");
                }
            }

            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "loc-dump.txt"), dump);

            probe.Close();
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunSelfTestAsync();
            return;
        }

        if (e.Args.Contains("--screenshot-menu", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow();
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Show();
            window.UpdateLayout();
            window.PrepareRowMenuForTest();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            SaveMenuPng(window.RowMenuForTest, PathArg(e.Args, "--screenshot-menu", "menu.png"));
            window.RowMenuForTest.IsOpen = false;
            window.Close();
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--screenshot-cat-menu", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            window.UpdateLayout();

            var menu = window.CategoryMenuForTest("ArtCraft");
            menu.PlacementTarget = window;
            menu.IsOpen = true;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            SaveMenuPng(menu, PathArg(e.Args, "--screenshot-cat-menu", "cat-menu.png"));
            menu.IsOpen = false;
            window.Close();
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--probe", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunProbeAsync(e.Args);
            return;
        }

        if (e.Args.Contains("--screenshot-check", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunScreenshotCheckAsync(PathArg(e.Args, "--screenshot-check", "ui-check.png"));
            return;
        }

        if (e.Args.Contains("--screenshot-drag", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            window.UpdateLayout();
            window.PrepareDragPreviewForTest();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var root = window.Content as FrameworkElement ?? window;
            SavePng(root, PathArg(e.Args, "--screenshot-drag", "ui-drag.png"));
            window.Close();
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--screenshot", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow();

            if (e.Args.Contains("--packs-open", StringComparer.OrdinalIgnoreCase))
                window.OpenPacksForTest();

            var searchIndex = Array.FindIndex(e.Args,
                a => a.Equals("--search", StringComparison.OrdinalIgnoreCase));
            if (searchIndex >= 0 && searchIndex + 1 < e.Args.Length)
                window.SetSearchForTest(e.Args[searchIndex + 1]);


            var presetIndex = Array.FindIndex(e.Args,
                a => a.Equals("--preset", StringComparison.OrdinalIgnoreCase));
            if (presetIndex >= 0 && presetIndex + 1 < e.Args.Length)
                window.ApplyPresetByName(e.Args[presetIndex + 1]);

            var categoryIndex = Array.FindIndex(e.Args,
                a => a.Equals("--category", StringComparison.OrdinalIgnoreCase));
            if (categoryIndex >= 0 && categoryIndex + 1 < e.Args.Length)
                window.SelectCategory(e.Args[categoryIndex + 1]);

            var toggleIndex = Array.FindIndex(e.Args,
                a => a.Equals("--toggle", StringComparison.OrdinalIgnoreCase));
            if (toggleIndex >= 0 && toggleIndex + 1 < e.Args.Length)
            {
                if (e.Args[toggleIndex + 1].Equals("cats", StringComparison.OrdinalIgnoreCase))
                    window.ToggleCategoriesForTest();
                else if (e.Args[toggleIndex + 1].Equals("packs", StringComparison.OrdinalIgnoreCase))
                    window.TogglePacksForTest();
            }

            RenderToFile(window, PathArg(e.Args, "--screenshot", "ui-main.png"));
            return;
        }

        if (e.Args.Contains("--screenshot-edit", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RenderToFile(new EditItemWindow(null, new[] { "Браузеры", "Утилиты", "Игры" }),
                PathArg(e.Args, "--screenshot-edit", "ui-edit.png"));
            return;
        }

        new MainWindow().Show();
    }

    private static void SaveMenuPng(ContextMenu menu, string path)
    {
        var width = Math.Max(1, (int)Math.Ceiling(menu.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(menu.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(menu);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string PathArg(string[] args, string flag, string fallback)
    {
        var index = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Path.Combine(AppContext.BaseDirectory, fallback);
    }

    private static async Task RunProbeAsync(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--probe", StringComparison.OrdinalIgnoreCase));
        var url = index + 1 < args.Length ? args[index + 1] : "";
        var outPath = index + 2 < args.Length ? args[index + 2] : Path.Combine(AppContext.BaseDirectory, "probe.log");

        var lines = new List<string>();
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResp = await DownloadService.Http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead);
            lines.Add($"HEAD {(int)headResp.StatusCode} type={headResp.Content.Headers.ContentType} len={headResp.Content.Headers.ContentLength} url={headResp.RequestMessage?.RequestUri}");
        }
        catch (Exception ex)
        {
            lines.Add("HEAD EX: " + ex);
        }

        try
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, url);
            get.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var getResp = await DownloadService.Http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead);
            lines.Add($"GET {(int)getResp.StatusCode} type={getResp.Content.Headers.ContentType} len={getResp.Content.Headers.ContentLength} range={getResp.Content.Headers.ContentRange} url={getResp.RequestMessage?.RequestUri}");
        }
        catch (Exception ex)
        {
            lines.Add("GET EX: " + ex);
        }

        File.WriteAllLines(outPath, lines);
        Current.Shutdown(0);
    }

    private async Task RunScreenshotCheckAsync(string path)
    {
        var window = new MainWindow { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Show();
        window.UpdateLayout();

        try { await window.CheckLinksForTestAsync(); }
        catch { /* покажем как есть */ }

        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        var root = window.Content as FrameworkElement ?? window;
        SavePng(root, path);

        window.Close();
        Shutdown(0);
    }

    private void RenderToFile(Window window, string path)
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = 0;
        window.Top = 0;
        window.Show();
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        var root = window.Content as FrameworkElement ?? window;
        SavePng(root, path);

        window.Close();
        Shutdown(0);
    }

    private static void SavePng(FrameworkElement root, string path)
    {
        var width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        LogCrash(args.Exception);

        var headless = Environment.GetCommandLineArgs()
            .Any(a => a.StartsWith("--selftest", StringComparison.OrdinalIgnoreCase)
                      || a.StartsWith("--screenshot", StringComparison.OrdinalIgnoreCase));

        if (headless)
        {
            args.Handled = true;
            Current.Shutdown(2);
            return;
        }

        MessageBox.Show(args.Exception.ToString(), "SetupCrate — ошибка",
            MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true;
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }

    private static async Task RunSelfTestAsync()
    {
        var code = 2;
        try
        {
            code = await SelfTest.RunAsync(AppContext.BaseDirectory);
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.log"),
                    "EXCEPTION: " + ex);
            }
            catch { /* ignore */ }
        }

        Current.Shutdown(code);
    }
}
