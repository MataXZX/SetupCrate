using System.Windows;
using System.Windows.Controls;
using SetupCrate.Models;
using SetupCrate.Services;

namespace SetupCrate;

public partial class EditItemWindow : Window
{
    private readonly string _id;
    private readonly bool _isNew;
    private readonly List<string> _categories;

    public CatalogItem? Result { get; private set; }

    public EditItemWindow(CatalogItem? source, IEnumerable<string> categories)
    {
        InitializeComponent();

        _isNew = source == null;
        _id = source?.Id ?? Guid.NewGuid().ToString("N");

        _categories = categories.Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (source != null)
        {
            TxtName.Text = source.Name;
            TxtSite.Text = source.Site;
            TxtUrl.Text = source.Url;
            TxtCategory.Text = source.Category;
            TxtNotes.Text = source.Notes;
            TxtSilent.Text = source.SilentArgs;
            TxtTarget.Text = source.TargetDir;
            TxtHash.Text = source.Sha256;
        }
        else
        {
            TxtCategory.Text = "Без категории";
        }

        Loaded += (_, _) => TxtName.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        var url = TxtUrl.Text.Trim();

        if (name.Length == 0)
        {
            MessageBox.Show("Укажи название программы.", "SetupCrate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            MessageBox.Show("Нужна полная ссылка на файл, начиная с https:// (например, https://site.com/app-x64.exe).",
                "SetupCrate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var site = TxtSite.Text.Trim();
        if (site.Length > 0 &&
            (!Uri.TryCreate(site, UriKind.Absolute, out var siteUri) ||
             (siteUri.Scheme != Uri.UriSchemeHttps && siteUri.Scheme != Uri.UriSchemeHttp)))
        {
            MessageBox.Show("Сайт должен быть полной ссылкой, начиная с https://", "SetupCrate",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var category = TxtCategory.Text.Trim();
        if (category.Length == 0) category = "Без категории";

        Result = new CatalogItem
        {
            Id = _id,
            Name = name,
            Site = site,
            Url = url,
            Category = category,
            Notes = TxtNotes.Text.Trim(),
            SilentArgs = TxtSilent.Text.Trim(),
            TargetDir = TxtTarget.Text.Trim(),
            Sha256 = TxtHash.Text.Trim()
        };

        DialogResult = true;
    }

    private void BtnCategories_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var category in _categories.DefaultIfEmpty("Без категории"))
        {
            var item = new MenuItem { Header = category };
            item.Click += (_, _) => TxtCategory.Text = category;
            menu.Items.Add(item);
        }

        menu.PlacementTarget = BtnCategories;
        menu.IsOpen = true;
    }

    private async void BtnFind_Click(object sender, RoutedEventArgs e)
    {
        var source = !string.IsNullOrWhiteSpace(TxtUrl.Text) ? TxtUrl.Text
            : !string.IsNullOrWhiteSpace(TxtSite.Text) ? TxtSite.Text
            : "";

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            MessageBox.Show("Сначала укажи сайт или ссылку, с которой искать файлы.", "SetupCrate",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BtnFind.IsEnabled = false;
            BtnFind.Content = "Ищу…";
            var html = await DownloadService.Http.GetStringAsync(uri);
            var links = LinkExtractor.Extract(html, uri);

            if (links.Count == 0)
            {
                MessageBox.Show("На этой странице не нашлось ссылок .exe/.msi/.zip. Открой сайт вручную и скопируй прямую ссылку.",
                    "SetupCrate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new LinkPickerWindow(links) { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedLink != null)
                TxtUrl.Text = picker.SelectedLink;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось загрузить страницу: " + ex.Message, "SetupCrate",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnFind.Content = "Найти ссылки на странице…";
            BtnFind.IsEnabled = true;
        }
    }
}
