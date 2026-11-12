using System.Windows;

namespace SetupCrate.Services;

public enum AppTheme
{
    Light,
    Dark,
    Glass
}

public static class ThemeManager
{
    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        Current = theme;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var old = dictionaries.FirstOrDefault(d =>
            d.Source != null && IsThemeDictionary(d.Source.OriginalString));

        var replacement = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative)
        };

        if (old != null) dictionaries[dictionaries.IndexOf(old)] = replacement;
        else dictionaries.Insert(0, replacement);
    }

    private static bool IsThemeDictionary(string source) =>
        source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
        source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
        source.EndsWith("Glass.xaml", StringComparison.OrdinalIgnoreCase);

    public static AppTheme Next() => Current switch
    {
        AppTheme.Light => AppTheme.Dark,
        AppTheme.Dark => AppTheme.Glass,
        _ => AppTheme.Light
    };

    public static string DisplayName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "Светлая",
        AppTheme.Dark => "Тёмная",
        _ => "Стекло"
    };

    public static AppTheme Parse(string? value) => value switch
    {
        "Dark" => AppTheme.Dark,
        "Glass" => AppTheme.Glass,
        _ => AppTheme.Light
    };
}
