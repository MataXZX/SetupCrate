using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

using SetupCrate.Services;

namespace SetupCrate;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly string _baseDir;
    private readonly Action _import;
    private readonly Action _export;
    private readonly Action _languageChanged;

    public SettingsWindow(AppSettings settings, string baseDir, Action import, Action export, Action languageChanged)
    {
        InitializeComponent();

        _settings = settings;
        _baseDir = baseDir;
        _import = import;
        _export = export;
        _languageChanged = languageChanged;

        TxtVersion.Text = Loc.F("Текущая версия: {0}", CurrentVersion());

        UpdateLanguageButtons();
    }

    private static Version CurrentVersion()
    {
        var text = typeof(SettingsWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)
            ?? "1.0";

        var plus = text.IndexOf('+');
        if (plus >= 0) text = text[..plus];

        return Version.TryParse(text, out var version) ? version : new Version(1, 0);
    }

    private static string CurrentVersionText() => CurrentVersion().ToString(3);


    private void UpdateLanguageButtons()
    {
        Mark(BtnAuto, _settings.Language == "auto");
        Mark(BtnRu, _settings.Language == "ru");
        Mark(BtnEn, _settings.Language == "en");
    }

    private void Mark(Button button, bool selected) =>
        button.Style = (Style)FindResource(selected ? "PrimaryBtn" : "GhostBtn");

    private void SetLanguage(string value)
    {
        _settings.Language = value;
        Loc.Apply(value switch
        {
            "ru" => AppLang.Ru,
            "en" => AppLang.En,
            _ => Loc.Detect()
        });
        SettingsStore.Save(_baseDir, _settings);


        UpdateLanguageButtons();
        TxtVersion.Text = Loc.F("Текущая версия: {0}", CurrentVersion());
        _languageChanged();
    }

    private void BtnAuto_Click(object sender, RoutedEventArgs e) => SetLanguage("auto");

    private void BtnRu_Click(object sender, RoutedEventArgs e) => SetLanguage("ru");

    private void BtnEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");

    private void BtnImport_Click(object sender, RoutedEventArgs e) => _import();

    private void BtnExport_Click(object sender, RoutedEventArgs e) => _export();

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        TxtUpdateStatus.Text = Loc.T("Проверяю…");

        try
        {
            var info = await UpdateChecker.CheckAsync(CurrentVersion());

            if (info is null)
            {
                TxtUpdateStatus.Text = Loc.T("Установлена последняя версия.");
                return;
            }

            TxtUpdateStatus.Text = Loc.F("Доступна новая версия {0}.", info.Version);

            var answer = MessageBox.Show(this,
                Loc.F("Доступна новая версия {0}. Открыть страницу загрузки?", info.Version),
                "SetupCrate", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(info.AssetUrl ?? info.PageUrl) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = Loc.F("Не удалось проверить обновления: {0}", ex.Message);
        }
        finally
        {
            BtnCheckUpdates.IsEnabled = true;
        }
    }

    private void BtnAbout_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
