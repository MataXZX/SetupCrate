using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SetupCrate.Models;
using SetupCrate.Services;
using Microsoft.Win32;

namespace SetupCrate;

public partial class MainWindow : Window
{
    private const string AllCategory = "Все";
    private const string NoCategory = "Без категории";

    private readonly string _baseDir = AppContext.BaseDirectory;
    private readonly CatalogStore _store;
    private readonly ObservableCollection<CatalogItem> _items = new();
    private readonly ObservableCollection<CategoryEntry> _categories = new();
    private readonly Dictionary<string, List<CatalogItem>> _packs = new();
    private readonly DownloadService _downloader = new();
    private readonly AppSettings _settings;
    private readonly ContextMenu _itemMenu = new();
    private CatalogFile _catalog;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private CatalogItem? _menuTarget;
    private bool _installedBusy;
    private DateTime _lastInstalledScan = DateTime.MinValue;
    private Point _dragStart;
    private CategoryEntry? _dragEntry;
    private Rect _restoreBounds;
    private bool _isMaximized;

    private Preset? _activePreset;
    private CommunityCatalog? _activeCommunity;
    private ICollectionView? _communityView;
    private readonly ObservableCollection<CommunityCatalog> _community = new();
    private readonly Dictionary<string, List<CatalogItem>> _communityCache = new();
    private IReadOnlyList<MarketEntry> _market = Array.Empty<MarketEntry>();
    private MarketEntry? _selectedEntry;
    private CommunityCatalog? _storePack;
    private ICollectionView? _packView;

    private static string AppVersion =>
        typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString(3)
        ?? "1.0";

    public MainWindow()
    {
        InitializeComponent();
        Title = $"SetupCrate {AppVersion}";

        _settings = SettingsStore.Load(_baseDir);
        _store = new CatalogStore(_baseDir);
        _catalog = _store.Load();
        foreach (var item in _catalog.Items)
        {
            item.PropertyChanged += Item_PropertyChanged;
            _items.Add(item);
        }

        var view = CollectionViewSource.GetDefaultView(_items);
        view.Filter = FilterItem;
        ItemsList.ItemsSource = view;

        ListCategories.ItemsSource = _categories;
        PacksList.ItemsSource = Presets.All;
        BtnPacksToggle.IsChecked = true;
        PacksList.Visibility = Visibility.Visible;
        PacksChevron.Text = "▾";

        BuildItemMenu();

        BtnCommunityToggle.IsChecked = false;
        CommunityPanel.Visibility = Visibility.Collapsed;
        CommunityList.ItemsSource = _community;
        ReloadCommunity();
        UpdateCommunityEmpty();
        ItemsList.ContextMenu = _itemMenu;

        RebuildCategories();
        UpdateThemeButton();
        UpdateEmptyState();

        try { Directory.CreateDirectory(DownloadDir); } catch {  }

        SetStatus(_items.Count == 0
            ? Loc.T("Каталог пуст. Нажми «Добавить»: название + прямая ссылка на установщик.")
            : Loc.F("Записей: {0}. Папка загрузок: {1}", _items.Count, DownloadDir));

        _ = RefreshInstalledAsync();
    }

    private string DownloadDir => Path.IsPathRooted(_catalog.DownloadDir)
        ? _catalog.DownloadDir
        : Path.Combine(_baseDir, _catalog.DownloadDir);

    private static string CatOf(CatalogItem item) =>
        string.IsNullOrWhiteSpace(item.Category) ? NoCategory : item.Category.Trim();

    private static bool IsRealCategory(string name) =>
        !string.Equals(name, AllCategory, StringComparison.CurrentCultureIgnoreCase) &&
        !string.Equals(name, NoCategory, StringComparison.CurrentCultureIgnoreCase);

    private void Save()
    {
        _catalog.Items = _items.ToList();
        try { _store.Save(_catalog); }
        catch (Exception ex) { SetStatus(Loc.F("Не удалось сохранить каталог: {0}", ex.Message)); }
    }

    private void RebuildCategories()
    {
        var selectedName = (ListCategories.SelectedItem as CategoryEntry)?.Name ?? AllCategory;

        var counts = _items.GroupBy(CatOf)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.CurrentCultureIgnoreCase);

        var names = new List<string>();
        foreach (var stored in _catalog.Categories)
        {
            var name = stored.Trim();
            if (string.IsNullOrWhiteSpace(name) || !IsRealCategory(name)) continue;
            if (!names.Contains(name, StringComparer.CurrentCultureIgnoreCase)) names.Add(name);
        }

        foreach (var name in counts.Keys.OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!IsRealCategory(name)) continue;
            if (!names.Contains(name, StringComparer.CurrentCultureIgnoreCase)) names.Add(name);
        }

        if (counts.ContainsKey(NoCategory) && !names.Contains(NoCategory, StringComparer.CurrentCultureIgnoreCase))
            names.Add(NoCategory);

        _catalog.Categories = names.Where(IsRealCategory).ToList();

        _categories.Clear();
        _categories.Add(new CategoryEntry(AllCategory, _items.Count));
        foreach (var name in names)
            _categories.Add(new CategoryEntry(name, counts.GetValueOrDefault(name)));

        ListCategories.SelectedItem = _categories.FirstOrDefault(c => c.Name == selectedName) ?? _categories[0];
        UpdateToggleLabels();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not CatalogItem item) return false;

        var category = (ListCategories.SelectedItem as CategoryEntry)?.Name;
        if (!string.IsNullOrEmpty(category) && category != AllCategory &&
            !string.Equals(CatOf(item), category, StringComparison.CurrentCultureIgnoreCase))
            return false;

        return MatchesSearch(item);
    }

    private bool MatchesSearch(CatalogItem item)
    {
        var query = TxtSearch.Text.Trim();
        if (query.Length == 0) return true;

        return item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || item.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || item.Site.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || item.Url.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || item.Notes.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool PackFilter(object obj) => obj is CatalogItem item && MatchesSearch(item);

    private void RefreshView()
    {
        if (_activePreset != null)
        {
            _packView?.Refresh();
            return;
        }

        CollectionViewSource.GetDefaultView(_items).Refresh();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (EmptyOverlay == null) return;
        EmptyOverlay.Visibility = _activePreset == null && _items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private IEnumerable<string> CategoryNames() =>
        _catalog.Categories.Concat(_items.Select(CatOf))
            .Where(c => !string.IsNullOrWhiteSpace(c) && IsRealCategory(c))
            .Distinct(StringComparer.CurrentCultureIgnoreCase);

    private List<CatalogItem> GetPackItems(Preset preset)
    {
        if (_packs.TryGetValue(preset.Name, out var cached)) return cached;

        var items = preset.Apps.Select(app => new CatalogItem
        {
            Name = app.Name,
            Url = app.Url,
            Site = app.Site,
            Notes = Loc.T(app.Notes),
            Category = "Пакет"
        }).ToList();

        foreach (var item in items) item.PropertyChanged += Item_PropertyChanged;
        _packs[preset.Name] = items;
        return items;
    }

    private void ShowPack(Preset preset)
    {
        _activePreset = preset;

        var items = GetPackItems(preset);
        _packView = new CollectionViewSource { Source = items }.View;
        _packView.Filter = PackFilter;
        ItemsList.ItemsSource = _packView;

        SetBusy(_busy);
        UpdateEmptyState();
        UpdateToggleLabels();
        SetStatus(Loc.F("Открыт пакет «{0}». Это отдельный список — каталог не меняется.", preset.Name));

        _ = RefreshInstalledAsync();
    }

    private void ExitPack()
    {
        if (_activePreset == null && _activeCommunity == null) return;

        _activePreset = null;
        _activeCommunity = null;
        _packView = null;
        _communityView = null;
        ItemsList.ItemsSource = CollectionViewSource.GetDefaultView(_items);

        SetBusy(_busy);
        UpdateEmptyState();
        UpdateToggleLabels();
        SetStatus(Loc.T("Открыт каталог."));
    }

    private void Categories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LeaveStore();
        if (_activePreset != null || _activeCommunity != null) ExitPack();
        RefreshView();
    }

    private void SelectCategory(string name)
    {
        var entry = _categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (entry != null) ListCategories.SelectedItem = entry;
    }

    private void AddCategory()
    {
        var dialog = new InputDialog(Loc.T("Название новой категории:")) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var name = dialog.Value.Trim();
        if (!IsRealCategory(name) ||
            _categories.Any(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            SetStatus(Loc.T("Такая категория уже есть."));
            return;
        }

        _catalog.Categories.Add(name);
        Save();
        RebuildCategories();
        SelectCategory(name);
        SetStatus(Loc.F("Категория добавлена: {0}", name));
    }

    private void RenameCategory(string oldName)
    {
        var dialog = new InputDialog(Loc.F("Новое название для «{0}»:", oldName), oldName) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var newName = dialog.Value.Trim();
        if (string.Equals(newName, oldName, StringComparison.CurrentCultureIgnoreCase)) return;

        if (!IsRealCategory(newName) ||
            _categories.Any(c => string.Equals(c.Name, newName, StringComparison.CurrentCultureIgnoreCase)))
        {
            SetStatus(Loc.T("Такая категория уже есть."));
            return;
        }

        var index = _catalog.Categories.FindIndex(c => string.Equals(c, oldName, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0) _catalog.Categories[index] = newName;
        else _catalog.Categories.Add(newName);

        foreach (var item in _items.Where(i => string.Equals(CatOf(i), oldName, StringComparison.CurrentCultureIgnoreCase)))
            item.Category = newName;

        Save();
        RebuildCategories();
        SelectCategory(newName);
        SetStatus(Loc.F("Категория «{0}» переименована в «{1}».", oldName, newName));
    }

    private void DeleteCategory(string name)
    {
        var affected = _items.Count(i => string.Equals(CatOf(i), name, StringComparison.CurrentCultureIgnoreCase));
        var question = affected == 0
            ? $"Удалить пустую категорию «{name}»?"
            : $"Удалить категорию «{name}»?\n\nПрограмм в ней: {affected}. Они перейдут в «Без категории».";

        if (MessageBox.Show(question, "SetupCrate", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;

        foreach (var item in _items.Where(i => string.Equals(CatOf(i), name, StringComparison.CurrentCultureIgnoreCase)))
            item.Category = NoCategory;

        _catalog.Categories.RemoveAll(c => string.Equals(c, name, StringComparison.CurrentCultureIgnoreCase));
        Save();
        RebuildCategories();
        SelectCategory(AllCategory);
        SetStatus(Loc.F("Категория «{0}» удалена.", name));
    }

    private void MoveCategory(string name, int delta)
    {
        var list = _catalog.Categories.ToList();
        var index = list.FindIndex(c => string.Equals(c, name, StringComparison.CurrentCultureIgnoreCase));
        if (index < 0) return;

        var target = index + delta;
        if (target < 0 || target >= list.Count) return;

        (list[index], list[target]) = (list[target], list[index]);
        _catalog.Categories = list;
        Save();
        RebuildCategories();
        SelectCategory(name);
    }

    private void MoveCategoryBefore(string dragged, string? targetName)
    {
        var list = _catalog.Categories
            .Where(c => !string.Equals(c, dragged, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        var index = targetName == null
            ? list.Count
            : list.FindIndex(c => string.Equals(c, targetName, StringComparison.CurrentCultureIgnoreCase));
        if (index < 0) index = list.Count;

        list.Insert(index, dragged);
        _catalog.Categories = list;
        Save();
        RebuildCategories();
        SelectCategory(dragged);
    }

    private void Categories_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var entry = FindCategory(e.OriginalSource);
        var menu = BuildCategoryMenu(entry);
        menu.PlacementTarget = ListCategories;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildCategoryMenu(CategoryEntry? entry)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItemFor(Loc.T("Новая категория…"), AddCategory));

        if (entry != null && IsRealCategory(entry.Name))
        {
            var name = entry.Name;
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor(Loc.T("Переименовать…"), () => RenameCategory(name)));
            menu.Items.Add(MenuItemFor(Loc.T("Удалить"), () => DeleteCategory(name)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor(Loc.T("Выше"), () => MoveCategory(name, -1)));
            menu.Items.Add(MenuItemFor(Loc.T("Ниже"), () => MoveCategory(name, 1)));
        }

        return menu;
    }

    private static CategoryEntry? FindCategory(object? source) =>
        FindCategoryItem(source)?.DataContext as CategoryEntry;

    private static ListBoxItem? FindCategoryItem(object? source)
    {
        var current = source as DependencyObject;
        while (current != null && current is not ListBoxItem)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);

        return current as ListBoxItem;
    }

    private void HideDropLine()
    {
        if (DropLine != null) DropLine.Visibility = Visibility.Collapsed;
    }

    private string? NextRealCategory(string name)
    {
        var list = _catalog.Categories;
        var index = list.FindIndex(c => string.Equals(c, name, StringComparison.CurrentCultureIgnoreCase));
        if (index < 0) return null;

        for (var i = index + 1; i < list.Count; i++)
            if (IsRealCategory(list[i])) return list[i];

        return null;
    }

    private void Categories_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragEntry = FindCategory(e.OriginalSource);
        _dragStart = e.GetPosition(this);
    }

    private void Categories_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragEntry == null) return;
        if (!IsRealCategory(_dragEntry.Name)) return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < 8 && Math.Abs(delta.Y) < 8) return;

        var entry = _dragEntry;
        _dragEntry = null;

        var container = ListCategories.ItemContainerGenerator.ContainerFromItem(entry) as ListBoxItem;
        if (container != null) container.Opacity = 0.45;

        try
        {
            DragDrop.DoDragDrop(ListCategories, entry.Name, DragDropEffects.Move);
        }
        finally
        {
            if (container != null) container.Opacity = 1;
            HideDropLine();
        }
    }

    private void Categories_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(string)))
        {
            e.Effects = DragDropEffects.None;
            HideDropLine();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;

        var item = FindCategoryItem(e.OriginalSource);
        if (item?.DataContext is CategoryEntry entry && IsRealCategory(entry.Name))
        {
            var below = e.GetPosition(item).Y > item.ActualHeight / 2;
            var topLeft = item.TranslatePoint(new Point(0, 0), DropLayer);
            Canvas.SetLeft(DropLine, 8);
            Canvas.SetTop(DropLine, topLeft.Y + (below ? item.ActualHeight : 0) - 1);
            DropLine.Width = Math.Max(40, DropLayer.ActualWidth - 16);
            DropLine.Visibility = Visibility.Visible;
        }
        else
        {
            HideDropLine();
        }

        e.Handled = true;
    }

    private void Categories_DragLeave(object sender, DragEventArgs e) => HideDropLine();

    private void Categories_Drop(object sender, DragEventArgs e)
    {
        HideDropLine();
        if (e.Data.GetData(typeof(string)) is not string dragged) return;

        string? insertBefore = null;
        var item = FindCategoryItem(e.OriginalSource);
        if (item?.DataContext is CategoryEntry entry && IsRealCategory(entry.Name))
        {
            var below = e.GetPosition(item).Y > item.ActualHeight / 2;
            insertBefore = below ? NextRealCategory(entry.Name) : entry.Name;
        }

        if (string.Equals(dragged, insertBefore, StringComparison.CurrentCultureIgnoreCase)) return;
        MoveCategoryBefore(dragged, insertBefore);
    }

    private IEnumerable<CatalogItem> AllKnownItems()
    {
        foreach (var item in _items) yield return item;
        foreach (var pack in _packs.Values)
            foreach (var item in pack)
                yield return item;
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastInstalledScan).TotalSeconds < 60) return;
        _ = RefreshInstalledAsync();
    }

    private async Task RefreshInstalledAsync()
    {
        if (_installedBusy) return;
        _installedBusy = true;
        try
        {
            var installed = await Task.Run(InstalledScanner.Scan);
            foreach (var item in AllKnownItems().ToList())
            {
                var match = installed.FirstOrDefault(a => InstalledScanner.Matches(item.Name, a.DisplayName));
                item.InstalledName = match?.DisplayName;
                item.InstalledVersion = match?.Version;
            }

            _lastInstalledScan = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Log(Loc.F("Не удалось прочитать список установленных программ: {0}", ex.Message));
        }
        finally
        {
            _installedBusy = false;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AcrylicHelper.Apply(this, ThemeManager.Current == AppTheme.Glass);
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Apply(ThemeManager.Next());
        AcrylicHelper.Apply(this, ThemeManager.Current == AppTheme.Glass);
        _settings.Theme = ThemeManager.Current.ToString();
        SettingsStore.Save(_baseDir, _settings);
        UpdateThemeButton();
    }

    private void UpdateThemeButton()
    {
        if (BtnTheme != null)
            BtnTheme.ToolTip = Loc.F("Тема: {0}", ThemeManager.DisplayName(ThemeManager.Current));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is TextBox or Button)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try { DragMove(); } catch {  }
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMax_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        if (_isMaximized)
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            _isMaximized = false;
        }
        else
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);

            var area = SystemParameters.WorkArea;
            Left = area.Left;
            Top = area.Top;
            Width = area.Width;
            Height = area.Height;
            _isMaximized = true;
        }

        UpdateMaxGlyph();
    }

    private void UpdateMaxGlyph()
    {
        if (BtnMax != null)
            BtnMax.Content = _isMaximized ? "❐" : "□";
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            _isMaximized = true;
            UpdateMaxGlyph();
        }
        else if (WindowState == WindowState.Normal && _isMaximized)
        {
            _isMaximized = false;
            UpdateMaxGlyph();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_activeCommunity != null)
        {
            SetStatus(Loc.T("В каталоге сообщества нельзя добавлять записи."));
            return;
        }
        if (_activePreset != null)
        {
            SetStatus(Loc.T("Сейчас открыт пакет. Вернись в каталог, чтобы добавить программу."));
            return;
        }

        var dialog = new EditItemWindow(null, CategoryNames()) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        RegisterItem(dialog.Result);
        SetStatus(Loc.F("Добавлено: {0}", dialog.Result.Name));
    }

    private void RegisterItem(CatalogItem item)
    {
        item.PropertyChanged += Item_PropertyChanged;
        _items.Add(item);

        if (IsRealCategory(CatOf(item)) &&
            !_catalog.Categories.Contains(CatOf(item), StringComparer.CurrentCultureIgnoreCase))
            _catalog.Categories.Add(CatOf(item));

        Save();
        RebuildCategories();
        UpdateEmptyState();
    }

    private void EditItem(CatalogItem current)
    {
        if (_activePreset != null)
        {
            SetStatus(Loc.T("Пакет — встроенный список, его записи не редактируются."));
            return;
        }

        var dialog = new EditItemWindow(current, CategoryNames()) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        var index = _items.IndexOf(current);
        if (index < 0) return;

        current.PropertyChanged -= Item_PropertyChanged;
        dialog.Result.PropertyChanged += Item_PropertyChanged;
        _items[index] = dialog.Result;

        if (IsRealCategory(CatOf(dialog.Result)) &&
            !_catalog.Categories.Contains(CatOf(dialog.Result), StringComparer.CurrentCultureIgnoreCase))
            _catalog.Categories.Add(CatOf(dialog.Result));

        Save();
        RebuildCategories();
        SetStatus(Loc.F("Сохранено: {0}", dialog.Result.Name));
    }

    private void DeleteItems(IReadOnlyList<CatalogItem> selected)
    {
        if (_activePreset != null)
        {
            SetStatus(Loc.T("Пакет — встроенный список, из него ничего не удаляется."));
            return;
        }

        if (selected.Count == 0) return;

        var answer = MessageBox.Show(Loc.F("Удалить из каталога: {0} шт.?", selected.Count),
            "SetupCrate", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        foreach (var item in selected)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            _items.Remove(item);
        }

        Save();
        RebuildCategories();
        UpdateEmptyState();
        SetStatus(Loc.F("Удалено: {0}", selected.Count));
    }

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_activeCommunity != null)
        {
            SetStatus(Loc.T("В каталоге сообщества нельзя удалять записи."));
            return;
        }
        if (_activePreset != null)
        {
            SetStatus(Loc.T("Это витрина пакета, а не каталог — удалять нечего. Вернись в каталог."));
            return;
        }

        var selected = _items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetStatus(Loc.T("Отметь галочками программы, которые нужно удалить."));
            return;
        }

        DeleteItems(selected);
    }

    private List<CatalogItem> CurrentList()
    {
        if (_activeCommunity != null) return GetCommunityItems(_activeCommunity);
        if (_activePreset != null) return GetPackItems(_activePreset);
        return _items.ToList();
    }

    private List<CatalogItem> CurrentSelection()
    {
        if (_activeCommunity != null)
            return GetCommunityItems(_activeCommunity).Where(i => i.IsSelected).ToList();

        if (_activePreset != null)
            return GetPackItems(_activePreset).Where(i => i.IsSelected).ToList();

        return _items.Where(i => i.IsSelected).ToList();
    }

    private void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        var list = CurrentSelection();
        if (list.Count == 0)
        {
            SetStatus(Loc.T("Отметь галочками, что скачать."));
            return;
        }

        _ = DownloadManyAsync(list);
    }

    private void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
    {
        var list = CurrentList();

        if (list.Count == 0)
        {
            SetStatus(Loc.T("Список пуст — скачивать нечего."));
            return;
        }

        _ = DownloadManyAsync(list);
    }

    private void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        var list = CurrentSelection();
        if (list.Count == 0)
        {
            SetStatus(Loc.T("Отметь галочками, что устанавливать."));
            return;
        }

        _ = InstallManyAsync(list);
    }

    private void BtnCheck_Click(object sender, RoutedEventArgs e)
    {
        var list = CurrentList();

        if (list.Count == 0)
        {
            SetStatus(Loc.T("Список пуст — проверять нечего."));
            return;
        }

        _ = CheckLinksAsync(list);
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DownloadDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DownloadDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось открыть папку: {0}", ex.Message));
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        SetStatus(Loc.T("Отмена…"));
    }

    private void BtnToggleAll_Click(object sender, RoutedEventArgs e)
    {
        if (_activePreset != null)
        {
            SetStatus(Loc.T("Сейчас открыт пакет — для него кнопка ниже, в блоке «Пакеты»."));
            return;
        }

        ToggleSelection(CategoryTargets(), Loc.T("Категории"));
    }

    private void BtnTogglePacks_Click(object sender, RoutedEventArgs e)
    {
        if (_activePreset == null)
        {
            SetStatus(Loc.T("Сначала открой пакет в блоке «Пакеты»."));
            return;
        }

        ToggleSelection(GetPackItems(_activePreset), "Пакет");
    }

    private List<CatalogItem> CategoryTargets()
    {
        var category = (ListCategories.SelectedItem as CategoryEntry)?.Name ?? AllCategory;

        var targets = category == AllCategory
            ? _items
            : _items.Where(i => string.Equals(CatOf(i), category, StringComparison.CurrentCultureIgnoreCase));

        return targets.ToList();
    }

    private void ToggleSelection(IReadOnlyList<CatalogItem> targets, string scope)
    {
        if (targets.Count == 0)
        {
            SetStatus(scope == "Пакет"
                ? Loc.T("В этом пакете нет программ.")
                : Loc.T("В этой категории нечего отмечать."));
            return;
        }

        var allSelected = targets.All(i => i.IsSelected);
        foreach (var item in targets) item.IsSelected = !allSelected;
        UpdateToggleLabels();
        SetStatus($"{scope}: {(allSelected ? "снято" : "отмечено")} {targets.Count}.");
    }

    private void UpdateToggleLabels()
    {
        if (BtnToggleAll == null) return;

        var categories = CategoryTargets();
        BtnToggleAll.Content = _activePreset == null &&
                               categories.Count > 0 && categories.All(i => i.IsSelected)
            ? Loc.T("Снять все")
            : Loc.T("Отметить все");

        var packs = _activePreset != null ? GetPackItems(_activePreset) : new List<CatalogItem>();
        if (BtnTogglePacks != null)
            BtnTogglePacks.Content = packs.Count > 0 && packs.All(i => i.IsSelected)
                ? Loc.T("Снять все")
                : Loc.T("Отметить все");
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogItem.IsSelected)) UpdateToggleLabels();
    }

    private void BtnPacksToggle_Click(object sender, RoutedEventArgs e)
    {
        var open = BtnPacksToggle.IsChecked == true;
        PacksList.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        PacksChevron.Text = open ? "▾" : "▸";
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        LeaveStore();
        if (sender is FrameworkElement { DataContext: Preset preset }) ShowPack(preset);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        RefreshView();
    }

    private void TxtSearch_GotFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateSearchPlaceholder();

    private void TxtSearch_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateSearchPlaceholder();

    private void UpdateSearchPlaceholder()
    {
        if (TxtPlaceholder == null) return;

        var show = string.IsNullOrEmpty(TxtSearch.Text) && !TxtSearch.IsKeyboardFocused;
        TxtPlaceholder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool _searchOpen;

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchOpen = !_searchOpen;

        var animation = new DoubleAnimation(
            _searchOpen ? 210 : 0,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        SearchHost.BeginAnimation(FrameworkElement.WidthProperty, animation);

        if (_searchOpen) TxtSearch.Focus();
        else
        {
            TxtSearch.Text = string.Empty;
            BtnSearch.Focus();
        }
    }

    private void TxtSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && _searchOpen)
        {
            BtnSearch_Click(BtnSearch, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _baseDir, ImportCatalog, ExportCatalog, ApplyLanguage)
        {
            Owner = this
        };

        window.ShowDialog();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Title = $"SetupCrate {AppVersion}";

        UpdateThemeButton();
        UpdateToggleLabels();
        BtnLogToggle.Content = TxtLog.Visibility == Visibility.Visible ? Loc.T("Скрыть лог") : Loc.T("Лог");

        RebuildCategories();
        foreach (var item in _items) item.NotifyMetaText();

        _packs.Clear();
        if (_activePreset != null) ShowPack(_activePreset);

        PacksList.ItemsSource = null;
        PacksList.ItemsSource = Presets.All;

        _itemMenu.Items.Clear();
        BuildItemMenu();

        BtnCommunityToggle.IsChecked = false;
        CommunityPanel.Visibility = Visibility.Collapsed;
        CommunityList.ItemsSource = _community;
        ReloadCommunity();
        UpdateCommunityEmpty();
        UpdateEmptyState();

        SetStatus(_items.Count == 0
            ? Loc.T("Каталог пуст. Нажми «Добавить»: название + прямая ссылка на установщик.")
            : Loc.F("Записей: {0}. Папка загрузок: {1}", _items.Count, DownloadDir));
    }

    private void BtnCommunity_Click(object sender, RoutedEventArgs e) => OpenStore();

    private void BtnThanks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://boosty.to/mataxzx/donate") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось открыть ссылку: {0}", ex.Message));
        }
    }

    private void LeaveStore()
    {
        if (StoreLayer.Visibility == Visibility.Visible) CloseStore();
    }

    private bool AskCommunityWarning()
    {
        var body = string.Join("\n\n",
            Loc.T("Эти каталоги создают другие пользователи и публикуют бесплатно. SetupCrate их не проверяет и не одобряет."),
            Loc.T("Мы не отвечаем за состав сборок, ссылки, установщики, безопасность, работоспособность и любой ущерб от установки или использования программ из таких каталогов."),
            Loc.T("Скачивание и установка — на ваш страх и риск. Перед установкой проверяйте, что это за программы и откуда они загружаются."));

        var window = new ConsentWindow(
            Loc.T("Каталоги сообщества"),
            body,
            Loc.T("Я понимаю, что SetupCrate не несёт ответственности за каталоги сообщества, и хочу продолжить."),
            Loc.T("Продолжить"),
            Loc.T("Запомнить мой выбор"))
        {
            Owner = this
        };

        if (window.ShowDialog() != true) return false;

        if (window.Remember)
        {
            _settings.CommunityWarningSeen = true;
            SettingsStore.Save(_baseDir, _settings);
        }

        return true;
    }

    private bool AskCommunityInstall(CommunityCatalog catalog)
    {
        var body = string.Join("\n\n",
            Loc.F("Этот каталог создал пользователь @{0}. SetupCrate не проверял приложения, ссылки и установщики в этой сборке и не несёт ответственности за их содержимое, безопасность, работоспособность и возможный вред.", catalog.Author),
            Loc.T("Установка выполняется на ваш страх и риск."));

        var window = new ConsentWindow(
            Loc.F("Установить каталог «{0}»?", catalog.Name),
            body,
            Loc.T("Я понимаю риски и всё равно хочу установить этот каталог."),
            Loc.T("Установить"),
            Loc.T("Запомнить мой выбор"))
        {
            Owner = this
        };

        if (window.ShowDialog() != true) return false;

        if (window.Remember)
        {
            _settings.CommunityInstallAccepted = true;
            SettingsStore.Save(_baseDir, _settings);
        }

        return true;
    }

    private void OpenStore()
    {
        if (!_settings.CommunityWarningSeen && !AskCommunityWarning()) return;

        InitStoreSort();

        StoreLayer.Visibility = Visibility.Visible;
        SetStoreView(StoreListPanel);
        _ = RefreshStoreAsync();
    }

    private void CloseStore() => StoreLayer.Visibility = Visibility.Collapsed;

    private void SetStoreView(UIElement panel)
    {
        StoreListPanel.Visibility = Visibility.Collapsed;
        StorePackPanel.Visibility = Visibility.Collapsed;
        StorePublishPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private async Task RefreshStoreAsync()
    {
        _market = await CommunityMarket.LoadAsync(_baseDir);
        ApplyStoreFilter();
    }

    private void ApplyStoreFilter()
    {
        var tokens = TxtStoreSearch.Text
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.TrimStart('#').Trim())
            .Where(token => token.Length > 0)
            .Select(token => token.ToLowerInvariant())
            .ToList();

        var filtered = _market
            .Where(entry => !_settings.HiddenPackIds.Contains(entry.Id))
            .Where(entry =>
            {
                if (tokens.Count == 0) return true;

                var haystack = (entry.Name + " " + entry.Id + " " + entry.Author + " " +
                                entry.Description + " " + string.Join(" ", entry.Tags)).ToLowerInvariant();

                return tokens.All(token => haystack.Contains(token));
            });

        var sorted = (CmbStoreSort?.SelectedIndex ?? 0) switch
        {
            1 => filtered.OrderByDescending(entry => entry.Downloads),
            2 => filtered.OrderByDescending(entry => entry.Likes),
            3 => filtered.OrderByDescending(entry => entry.Dislikes),
            4 => filtered.OrderByDescending(entry => entry.Verified).ThenByDescending(entry => entry.Likes),
            5 => filtered.OrderByDescending(entry => entry.HasChannel).ThenByDescending(entry => entry.Likes),
            _ => filtered
        };

        var result = sorted.ToList();
        StoreList.ItemsSource = result;
        TxtStoreEmpty.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InitStoreSort()
    {
        if (CmbStoreSort.ItemsSource != null) return;

        CmbStoreSort.ItemsSource = new[]
        {
            Loc.T("По умолчанию"),
            Loc.T("По скачиваниям"),
            Loc.T("По лайкам"),
            Loc.T("По дизлайкам"),
            Loc.T("Сначала проверенные"),
            Loc.T("Сначала от ютуберов")
        };
        CmbStoreSort.SelectedIndex = 0;
    }

    private void CmbStoreSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyStoreFilter();

    private void Tag_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: string tag } || string.IsNullOrWhiteSpace(tag)) return;

        TxtStoreSearch.Text = "#" + tag;
        TxtStoreSearch.Focus();
        ApplyStoreFilter();
    }

    private void Channel_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: string url } || string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось открыть ссылку: {0}", ex.Message));
        }
    }

    private void TxtStoreSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyStoreFilter();
        UpdateStorePlaceholder();
    }

    private void TxtStoreSearch_GotFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateStorePlaceholder();

    private void TxtStoreSearch_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateStorePlaceholder();

    private void UpdateStorePlaceholder()
    {
        if (TxtStorePlaceholder == null) return;

        var show = string.IsNullOrEmpty(TxtStoreSearch.Text) && !TxtStoreSearch.IsKeyboardFocused;
        TxtStorePlaceholder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnStoreBack_Click(object sender, RoutedEventArgs e)
    {
        if (StorePackPanel.Visibility == Visibility.Visible || StorePublishPanel.Visibility == Visibility.Visible)
            SetStoreView(StoreListPanel);
        else
            CloseStore();
    }

    private void BtnStoreBackToList_Click(object sender, RoutedEventArgs e) => SetStoreView(StoreListPanel);

    private async void StoreCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MarketEntry entry }) return;

        _selectedEntry = entry;
        _storePack = null;

        TxtPackName.Text = entry.Name;
        TxtPackAuthor.Text = entry.Author;
        TxtPackAuthorNote.Text = Loc.F("Сборка пользователя @{0}. Не проверена SetupCrate. Мы не несём ответственности за её содержимое и безопасность.", entry.Author);
        PackVerifiedBadge.Visibility = entry.Verified ? Visibility.Visible : Visibility.Collapsed;
        BtnPackChannel.Tag = entry.ChannelUrl;
        BtnPackChannel.Visibility = entry.HasChannel ? Visibility.Visible : Visibility.Collapsed;


        TxtPackStats.Text = entry.RatingText + "    " + entry.DownloadsText;
        TxtPackTags.Text = entry.TagsText;
        TxtPackDescription.Text = entry.Description;
        BtnPackId.Content = "ID: " + entry.Id;
        PackAppsList.ItemsSource = null;
        TxtPackStatus.Text = "";

        SetStoreView(StorePackPanel);

        var local = CommunityStore.LoadAll(_baseDir)
            .FirstOrDefault(c => c.Id == entry.Id || c.Name == entry.Name);

        if (local != null)
        {
            _storePack = local;
        }
        else if (!string.IsNullOrWhiteSpace(entry.DownloadUrl))
        {
            var json = await CommunityMarket.DownloadAsync(entry.DownloadUrl);
            if (json != null) _storePack = CommunityStore.Parse(json);
        }

        if (_storePack != null)
        {
            PackAppsList.ItemsSource = _storePack.Apps;
            TxtPackStatus.Text = Loc.F("Программ: {0}", _storePack.Apps.Count);
        }
        else
        {
            TxtPackStatus.Text = Loc.T("Пак недоступен: нет соединения с магазином.");
        }
    }

    private void BtnPackDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_storePack is null) return;
        if (!_settings.CommunityInstallAccepted && !AskCommunityInstall(_storePack)) return;

        try
        {
            CommunityStore.Save(_baseDir, _storePack);
            ReloadCommunity();
            _ = CommunityMarket.TrackDownloadAsync(_storePack.Id);
            TxtPackStatus.Text = Loc.F("Пак добавлен: {0}", _storePack.Name);
        }
        catch (Exception ex)
        {
            TxtPackStatus.Text = Loc.F("Не удалось добавить пак: {0}", ex.Message);
        }
    }

    private static string DescribePublishError(string raw)
    {
        var text = (raw ?? "").Trim();

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("error", out var error))
                text = error.GetString() ?? text;
        }
        catch
        {
        }

        if (text.Contains("daily limit", StringComparison.OrdinalIgnoreCase))
            return Loc.T("Лимит публикаций на сегодня исчерпан. Попробуй позже.");

        if (text.Contains("too many attempts", StringComparison.OrdinalIgnoreCase))
            return Loc.T("Слишком много попыток. Подожди немного.");

        return text;
    }

    private void BtnPackId_Click(object sender, RoutedEventArgs e)
    {
        var id = _storePack?.Id ?? _selectedEntry?.Id;
        if (string.IsNullOrWhiteSpace(id)) return;

        try
        {
            Clipboard.SetText(id);
            TxtPackStatus.Text = Loc.F("ID скопирован: {0}", id);
        }
        catch (Exception ex)
        {
            TxtPackStatus.Text = Loc.F("Не удалось скопировать: {0}", ex.Message);
        }
    }

    private void BtnCommunityDelete_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: CommunityCatalog catalog }) return;

        var answer = MessageBox.Show(this,
            Loc.F("Удалить пак «{0}»?", catalog.Name),
            Loc.T("Удаление пака"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            CommunityStore.Delete(_baseDir, catalog);
            if (_activeCommunity?.Id == catalog.Id) ExitPack();
            ReloadCommunity();
            SetStatus(Loc.F("Пак удалён: {0}", catalog.Name));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось удалить пак: {0}", ex.Message));
        }
    }


    private DispatcherTimer? _toastTimer;

    private void ShowToast(string text)
    {
        if (StoreToast == null || TxtStoreToast == null) return;

        TxtStoreToast.Text = text;
        StoreToast.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer?.Stop();
            StoreToast.Visibility = Visibility.Collapsed;
        };
        _toastTimer.Start();
    }

    private async void BtnPackReport_Click(object sender, RoutedEventArgs e)
    {
        var id = _storePack?.Id ?? _selectedEntry?.Id;
        if (string.IsNullOrWhiteSpace(id)) return;

        var name = _storePack?.Name ?? _selectedEntry?.Name ?? "";
        var author = _storePack?.Author ?? _selectedEntry?.Author ?? "";

        var window = new ReportWindow(name, author, id) { Owner = this };
        if (window.ShowDialog() != true) return;

        var ok = await CommunityMarket.ReportAsync(id, window.ReasonTitle, window.Comment);

        if (window.HidePack && !_settings.HiddenPackIds.Contains(id))
        {
            _settings.HiddenPackIds.Add(id);
            SettingsStore.Save(_baseDir, _settings);
            ReloadCommunity();
        }

        SetStoreView(StoreListPanel);
        _ = RefreshStoreAsync();

        ShowToast(ok
            ? Loc.T("Спасибо за сигнал! До окончания проверки не рекомендуем устанавливать эту сборку.")
            : Loc.T("Не удалось отправить жалобу. Проверь соединение и попробуй ещё раз."));
    }

    private void BtnStorePublish_Click(object sender, RoutedEventArgs e)
    {
        TxtPublishName.Text = "";
        TxtPublishAuthor.Text = Environment.UserName;
        TxtPublishDescription.Text = "";
        TxtPublishTags.Text = "";
        TxtPublishStatus.Text = "";

        CmbPublishCategory.ItemsSource = _categories
            .Select(c => c.Name)
            .Where(name => IsRealCategory(name) &&
                           _items.Any(i => string.Equals(CatOf(i), name, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
        CmbPublishCategory.SelectedIndex = -1;

        UpdatePublishState();
        SetStoreView(StorePublishPanel);
    }

    private void CmbPublishCategory_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePublishState();

    private void TxtPublishName_TextChanged(object sender, TextChangedEventArgs e) => UpdatePublishState();

    private List<CatalogItem> PublishItems()
    {
        if (CmbPublishCategory.SelectedItem is not string category) return new List<CatalogItem>();

        return _items
            .Where(i => string.Equals(CatOf(i), category, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
    }

    private void UpdatePublishState()
    {
        if (TxtPublishApps == null || BtnPublishSend == null) return;

        var items = PublishItems();

        TxtPublishApps.Text = CmbPublishCategory.SelectedItem is string category
            ? Loc.F("Программ в паке: {0}", items.Count)
            : Loc.T("Выбери категорию, которой хочешь поделиться.");

        BtnPublishSend.IsEnabled = items.Count > 0 && !string.IsNullOrWhiteSpace(TxtPublishName.Text);
    }

    private bool AskPublishConsent()
    {
        var bullets = string.Join("\n",
            "• " + Loc.T("самостоятельно подготовили её содержимое;"),
            "• " + Loc.T("указали действительные ссылки на официальные источники приложений;"),
            "• " + Loc.T("не публикуете пиратское, вредоносное или иное незаконное содержимое;"),
            "• " + Loc.T("ознакомлены с тем, что SetupCrate не проверяет пользовательские сборки и не несёт ответственности за их содержимое и последствия использования."));

        var body = string.Join("\n\n",
            Loc.T("Публикуя сборку, вы подтверждаете, что:"),
            bullets,
            Loc.T("Вы несёте ответственность за свою публикацию. SetupCrate вправе скрыть или удалить сборку, а также заблокировать доступ пользователя к публикации."));

        var window = new ConsentWindow(
            Loc.T("Ответственность за публикацию"),
            body,
            Loc.T("Я принимаю ответственность за публикацию."),
            Loc.T("Опубликовать"),
            null)
        {
            Owner = this
        };

        return window.ShowDialog() == true;
    }

    private async void BtnPublishSend_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPublishCategory.SelectedItem is not string)
        {
            TxtPublishStatus.Text = Loc.T("Выбери категорию для публикации.");
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtPublishName.Text))
        {
            TxtPublishStatus.Text = Loc.T("Укажи название пака.");
            return;
        }

        var items = PublishItems();
        if (items.Count == 0)
        {
            TxtPublishStatus.Text = Loc.T("В этой категории нет программ.");
            return;
        }

        if (!_settings.PublishConsentAccepted && !AskPublishConsent()) return;

        var catalog = CommunityStore.FromCatalog(TxtPublishName.Text, TxtPublishAuthor.Text, items);
        catalog.Id = string.Empty;
        catalog.Description = TxtPublishDescription.Text.Trim();
        catalog.Tags = TxtPublishTags.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        TxtPublishStatus.Text = Loc.T("Отправляю…");

        var (ok, message) = await CommunityMarket.PublishAsync(CommunityStore.ToJson(catalog));

        TxtPublishStatus.Text = ok
            ? Loc.F("Пак отправлен в магазин: {0}", TxtPublishName.Text)
            : Loc.F("Не удалось отправить: {0}", DescribePublishError(message));

        if (ok)
        {
            _settings.PublishConsentAccepted = true;
            SettingsStore.Save(_baseDir, _settings);
            await RefreshStoreAsync();
        }
    }

    private async void BtnVoteUp_Click(object sender, RoutedEventArgs e) => await VoteAsync(1);

    private async void BtnVoteDown_Click(object sender, RoutedEventArgs e) => await VoteAsync(-1);

    private async Task VoteAsync(int value)
    {
        if (_selectedEntry is null) return;

        var result = await CommunityMarket.VoteAsync(_selectedEntry.Id, value);
        if (result is { } counts)
        {
            _selectedEntry.Likes = counts.Likes;
            _selectedEntry.Dislikes = counts.Dislikes;
            TxtPackStats.Text = $"👍 {counts.Likes}    👎 {counts.Dislikes}    ↓ {_selectedEntry.Downloads}";
        }
    }


    private void BtnCommunityToggle_Click(object sender, RoutedEventArgs e)
    {
        var open = BtnCommunityToggle.IsChecked == true;
        CommunityPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        CommunityChevron.Text = open ? "▾" : "▸";
    }

    private void CommunityItem_Click(object sender, RoutedEventArgs e)
    {
        LeaveStore();
        if (sender is System.Windows.FrameworkElement { DataContext: CommunityCatalog catalog })
            ShowCommunity(catalog);
    }

    private void ReloadCommunity()
    {
        _community.Clear();
        foreach (var catalog in CommunityStore.LoadAll(_baseDir))
        {
            if (_settings.HiddenPackIds.Contains(catalog.Id)) continue;
            _community.Add(catalog);
        }
        UpdateCommunityEmpty();
    }

    private List<CatalogItem> GetCommunityItems(CommunityCatalog catalog)
    {
        if (_communityCache.TryGetValue(catalog.Id, out var cached)) return cached;

        var items = catalog.Apps.Select(app => new CatalogItem
        {
            Name = app.Name,
            Url = app.Url,
            Site = app.Site,
            Notes = app.Notes,
            Category = Loc.T("Сообщество")
        }).ToList();

        foreach (var item in items) item.PropertyChanged += Item_PropertyChanged;
        _communityCache[catalog.Id] = items;
        return items;
    }

    private void ShowCommunity(CommunityCatalog catalog)
    {
        _activePreset = null;
        _activeCommunity = catalog;

        var items = GetCommunityItems(catalog);
        _communityView = new CollectionViewSource { Source = items }.View;
        _communityView.Filter = PackFilter;
        ItemsList.ItemsSource = _communityView;

        UpdateEmptyState();
        SetStatus(Loc.F("Открыт каталог сообщества «{0}» (ID: {1}).", catalog.Name, catalog.Id));
        _ = RefreshInstalledAsync();
    }
    private static string SafeCommunityFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = "catalog";
        return clean + ".json";
    }

    private void UpdateCommunityEmpty() =>
        CommunityEmpty.Visibility = CommunityList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void BtnLogToggle_Click(object sender, RoutedEventArgs e)
    {
        var show = TxtLog.Visibility != Visibility.Visible;
        TxtLog.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BtnLogToggle.Content = show ? Loc.T("Скрыть лог") : Loc.T("Лог");
    }

    private void BuildItemMenu()
    {
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Скачать"), () =>
        {
            if (_menuTarget is { } item) _ = DownloadManyAsync(new[] { item });
        }));
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Установить"), () =>
        {
            if (_menuTarget is { } item) _ = InstallManyAsync(new[] { item });
        }));
        _itemMenu.Items.Add(new Separator());
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Открыть сайт"), () =>
        {
            if (_menuTarget is { } item) OpenSite(item);
        }));
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Скопировать ссылку"), () =>
        {
            if (_menuTarget is not { } item) return;
            try
            {
                Clipboard.SetText(item.Url);
                SetStatus(Loc.F("Ссылка скопирована: {0}", item.Url));
            }
            catch (Exception ex)
            {
                SetStatus(Loc.F("Не удалось скопировать: {0}", ex.Message));
            }
        }));
        _itemMenu.Items.Add(new Separator());
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Изменить…"), () =>
        {
            if (_menuTarget is { } item) EditItem(item);
        }));
        _itemMenu.Items.Add(MenuItemFor(Loc.T("Удалить"), () =>
        {
            if (_menuTarget is { } item) DeleteItems(new[] { item });
        }));
    }

    private static MenuItem MenuItemFor(string header, Action action)
    {
        var menuItem = new MenuItem { Header = header };
        menuItem.Click += (_, _) => action();
        return menuItem;
    }

    private void ItemsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not FrameworkElement { DataContext: CatalogItem })
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);

        if (source is FrameworkElement { DataContext: CatalogItem item })
        {
            if (_itemMenu.IsOpen) _itemMenu.IsOpen = false;

            _menuTarget = item;
            _itemMenu.PlacementTarget = ItemsList;
            _itemMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            _itemMenu.HorizontalOffset = 0;
            _itemMenu.VerticalOffset = 0;

            var menu = _itemMenu;
            Dispatcher.BeginInvoke(new Action(() => menu.IsOpen = true),
                System.Windows.Threading.DispatcherPriority.Input);

            e.Handled = true;
        }
    }

    private void SiteLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CatalogItem item }) OpenSite(item);
    }

    private void OpenSite(CatalogItem item)
    {
        var url = string.IsNullOrWhiteSpace(item.Site) ? item.Url : item.Site;
        if (string.IsNullOrWhiteSpace(url)) return;

        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { SetStatus(Loc.F("Не удалось открыть сайт: {0}", ex.Message)); }
    }

    private void ExportCatalog()
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Экспорт каталога"),
            Filter = Loc.T("Каталог SetupCrate (*.json)|*.json"),
            FileName = $"setupcrate-catalog-{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _catalog.Items = _items.ToList();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_catalog, options));
            SetStatus(Loc.F("Экспортировано записей: {0} → {1}", _items.Count, dialog.FileName));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось экспортировать: {0}", ex.Message));
        }
    }

    private void ImportCatalog()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Импорт каталога"),
            Filter = Loc.T("Каталог SetupCrate (*.json)|*.json|Все файлы (*.*)|*.*")
        };
        if (dialog.ShowDialog() != true) return;

        CatalogFile? imported;
        try
        {
            imported = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Не удалось прочитать файл: {0}", ex.Message));
            return;
        }

        if (imported?.Items == null || imported.Items.Count == 0)
        {
            SetStatus(Loc.T("В выбранном файле нет записей."));
            return;
        }

        var answer = MessageBox.Show(
            $"В файле записей: {imported.Items.Count}.\n\n" +
            "«Да» — заменить текущий каталог.\n«Нет» — добавить к текущему (дубликаты пропущу).",
            Loc.T("Импорт каталога"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return;

        if (answer == MessageBoxResult.Yes)
        {
            foreach (var item in _items) item.PropertyChanged -= Item_PropertyChanged;
            _items.Clear();
            _catalog.Categories = imported.Categories?.ToList() ?? new List<string>();
            foreach (var item in imported.Items.Where(i => !string.IsNullOrWhiteSpace(i.Url)))
            {
                item.PropertyChanged += Item_PropertyChanged;
                _items.Add(item);
            }
        }
        else
        {
            foreach (var item in imported.Items.Where(i => !string.IsNullOrWhiteSpace(i.Url)))
            {
                if (_items.Any(i => i.Id == item.Id || (i.Name == item.Name && i.Url == item.Url))) continue;
                item.PropertyChanged += Item_PropertyChanged;
                _items.Add(item);
            }
        }

        Save();
        RebuildCategories();
        UpdateEmptyState();
        SetStatus(Loc.F("Импорт завершён. Записей сейчас: {0}.", _items.Count));
    }

    private async Task DownloadManyAsync(IReadOnlyList<CatalogItem> list)
    {
        if (_busy) return;

        _busy = true;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var dir = DownloadDir;

        try { Directory.CreateDirectory(dir); }
        catch (Exception ex)
        {
            SetStatus("Не удалось создать папку загрузок: " + ex.Message);
            _busy = false; SetBusy(false); _cts.Dispose(); _cts = null;
            return;
        }

        Progress.Minimum = 0;
        Progress.Maximum = list.Count;
        Progress.Value = 0;
        Log(Loc.F("Старт загрузки: {0} шт. → {1}", list.Count, dir));

        var semaphore = new SemaphoreSlim(3);
        var done = 0;
        var okCount = 0;
        var failedCount = 0;

        var tasks = list.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                SetItem(item, Loc.T("скачивание…"));
                var progress = new Progress<double>(p => SetItem(item, Loc.F("скачивание {0}", p.ToString("P0"))));
                var result = await _downloader.DownloadAsync(item, dir, progress, ct);

                item.FileName = result.FileName;
                item.LastDownloadUtc = DateTime.UtcNow;
                var status = SignatureStatus(item, result);
                SetItem(item, status, result.Bytes);
                Log($"{item.Name}: {CatalogItem.FormatBytes(result.Bytes)} → {result.FileName}; {status}");
                Interlocked.Increment(ref okCount);
            }
            catch (OperationCanceledException)
            {
                SetItem(item, Loc.T("отменено"));
            }
            catch (Exception ex)
            {
                SetItem(item, "✗ " + ex.Message);
                Log(Loc.F("{0}: ошибка — {1}", item.Name, ex.Message));
                Interlocked.Increment(ref failedCount);
            }
            finally
            {
                semaphore.Release();
                var finished = Interlocked.Increment(ref done);
                Progress.Value = finished;
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
            Save();
            Progress.Value = 0;
            SetStatus($"Готово: скачано {okCount} из {list.Count}" +
                      (failedCount > 0 ? $", ошибок {failedCount}" : "") + $". Папка: {dir}");
        }
    }

    private static string SignatureStatus(CatalogItem item, DownloadResult result)
    {
        var ext = Path.GetExtension(result.FilePath).ToLowerInvariant();

        string text;
        if (ext is ".zip" or ".7z")
        {
            text = Loc.T("✓ скачано");
        }
        else
        {
            var signature = SignatureChecker.Check(result.FilePath);
            text = signature.StartsWith("✓")
                ? Loc.T("✓ подпись ок")
                : signature.Contains("нет встроенной подписи")
                    ? Loc.T("⚠ без подписи")
                    : Loc.T("⚠ подпись не ок");
        }

        if (!string.IsNullOrWhiteSpace(item.Sha256))
        {
            if (result.Sha256 == null) text += " " + Loc.T("· sha256 не проверен");
            else if (string.Equals(result.Sha256, item.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)) text += " · sha256 ✓";
            else text += " · sha256 ✗";
        }

        if (result.Resumed) text += " " + Loc.T("· докачано");

        return text;
    }

    private async Task CheckLinksAsync(IReadOnlyList<CatalogItem> list)
    {
        if (_busy) return;

        _busy = true;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Progress.Minimum = 0;
        Progress.Maximum = list.Count;
        Progress.Value = 0;

        var semaphore = new SemaphoreSlim(5);
        var done = 0;
        var dead = 0;

        var tasks = list.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                SetItem(item, Loc.T("проверка…"));
                var check = await LinkChecker.CheckAsync(item, ct);
                SetItem(item, check.Message, check.Size);
                if (!check.Ok) Interlocked.Increment(ref dead);
            }
            catch (OperationCanceledException)
            {
                SetItem(item, Loc.T("отменено"));
            }
            catch (Exception ex)
            {
                SetItem(item, "✗ " + ex.Message);
                Interlocked.Increment(ref dead);
            }
            finally
            {
                semaphore.Release();
                var finished = Interlocked.Increment(ref done);
                Progress.Value = finished;
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
            Progress.Value = 0;
            SetStatus(dead == 0 ? Loc.T("Все ссылки отвечают.") : Loc.F("Проверено {0}, проблемных: {1}.", list.Count, dead));
        }
    }

    private async Task InstallManyAsync(IReadOnlyList<CatalogItem> list)
    {
        if (_busy) return;

        _busy = true;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            foreach (var item in list)
            {
                ct.ThrowIfCancellationRequested();

                var path = ResolveLocalFile(item);
                if (path == null)
                {
                    SetItem(item, Loc.T("✗ файл не скачан — сначала «Скачать»"));
                    continue;
                }

                SetItem(item, Loc.T("запуск установщика…"));
                try
                {
                    var code = await ProcessRunner.RunAsync(path, item.SilentArgs, ct);
                    SetItem(item, Loc.F("✓ установщик завершён (код {0})", code));
                    Log(Loc.F("{0}: установщик завершён, код {1}", item.Name, code));
                }
                catch (OperationCanceledException)
                {
                    SetItem(item, Loc.T("отменено"));
                    break;
                }
                catch (Exception ex)
                {
                    SetItem(item, "✗ " + ex.Message);
                    Log(Loc.F("{0}: ошибка запуска — {1}", item.Name, ex.Message));
                }
            }
        }
        finally
        {
            _busy = false;
            SetBusy(false);
            _cts.Dispose();
            _cts = null;
            SetStatus(Loc.T("Установка завершена."));
        }
    }

    private string? ResolveLocalFile(CatalogItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.FileName))
        {
            var exact = Path.Combine(DownloadDir, item.FileName);
            if (File.Exists(exact)) return exact;
        }

        if (!Directory.Exists(DownloadDir)) return null;

        var safeName = item.Name;
        foreach (var c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');

        return Directory.GetFiles(DownloadDir)
            .Where(f => Path.GetFileName(f).StartsWith(safeName, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private void SetItem(CatalogItem item, string status, long? size = null)
    {
        item.StatusText = status;
        if (size is > 0) item.Size = size;
    }

    private void SetBusy(bool busy)
    {
        var catalogMode = _activePreset == null;

        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BtnAdd.IsEnabled = !busy && catalogMode;
        BtnDelete.IsEnabled = !busy && catalogMode;
        BtnToggleAll.IsEnabled = !busy && catalogMode;
        BtnTogglePacks.IsEnabled = !busy && !catalogMode;

        BtnDownload.IsEnabled = !busy;
        BtnDownloadAll.IsEnabled = !busy;
        BtnInstall.IsEnabled = !busy;
        BtnCheck.IsEnabled = !busy;
        BtnOpen.IsEnabled = !busy;

        BtnCancel.IsEnabled = busy;
    }

    private void SetStatus(string text)
    {
        TxtStatus.Text = text;
        Log(text);
    }

    private void Log(string text)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
        Save();
    }

}
