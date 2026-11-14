using System.Diagnostics;
using System.Reflection;
using System.Windows;

using SetupCrate.Services;

namespace SetupCrate;

public partial class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/MataXZX/SetupCrate";
    private const string DonateUrl = "https://boosty.to/mataxzx/donate";

    public AboutWindow()
    {
        InitializeComponent();
        TxtVersion.Text = Loc.F("Версия {0}", VersionText());
    }

    private static string VersionText()
    {
        var text = typeof(AboutWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)
            ?? "1.0";

        var plus = text.IndexOf('+');
        return plus >= 0 ? text[..plus] : text;
    }

    private void BtnSite_Click(object sender, RoutedEventArgs e) => Open(ProjectUrl);

    private void BtnThanks_Click(object sender, RoutedEventArgs e) => Open(DonateUrl);

    private void BtnLicense_Click(object sender, RoutedEventArgs e) => Open(ProjectUrl + "/blob/main/LICENSE");

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
