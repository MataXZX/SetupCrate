using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace SetupCrate.Services;

public enum AppLang
{
    Ru,
    En
}

/// <summary>Простая локализация: русский текст — ключ, при английском языке подменяется переводом.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public static AppLang Current { get; private set; } = AppLang.Ru;

    /// <summary>Растёт при каждой смене языка — по этому свойству обновляются привязки в XAML.</summary>
    public int Version { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Вызывается после смены языка, чтобы окна обновили тексты, заданные из кода.</summary>
    public static event Action? Changed;

    public static string T(string ru) =>
        Current == AppLang.En && Map.TryGetValue(ru, out var en) ? en : ru;

    public static string F(string ru, params object?[] args)
    {
        var template = T(ru);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public static AppLang Detect() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? AppLang.Ru
            : AppLang.En;

    public static void Apply(AppLang lang)
    {
        Current = lang;
        Instance.Version++;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Version)));
        Changed?.Invoke();
    }

    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        // ---------- главное окно ----------
        ["Поиск…"] = "Search…",
        ["Тема"] = "Theme",
        ["Лог"] = "Log",
        ["Скрыть лог"] = "Hide log",
        ["КАТЕГОРИИ"] = "CATEGORIES",
        ["ПАКЕТЫ"] = "PACKS",
        ["Отметить все"] = "Select all",
        ["Снять все"] = "Deselect all",
        ["Скачать отмеченные"] = "Download selected",
        ["Скачать всё"] = "Download all",
        ["Установить"] = "Install",
        ["Проверить ссылки"] = "Check links",
        ["Открыть папку"] = "Open folder",
        ["Добавить"] = "Add",
        ["Удалить"] = "Delete",
        ["Ещё"] = "More",
        ["Отмена"] = "Cancel",
        ["Сайт"] = "Site",
        ["Каталог пуст"] = "Catalog is empty",
        ["Добавь первую программу: название и прямая ссылка на установщик."] =
            "Add your first program: a name and a direct installer link.",
        ["Добавить программу"] = "Add program",
        ["Все"] = "All",
        ["Без категории"] = "No category",
        ["Пакет"] = "Pack",

        // ---------- темы ----------
        ["Светлая"] = "Light",
        ["Тёмная"] = "Dark",
        ["Стекло"] = "Glass",
        ["Тема: {0}"] = "Theme: {0}",

        // ---------- статусы и сообщения (главное окно) ----------
        ["Каталог пуст. Нажми «Добавить»: название + прямая ссылка на установщик."] =
            "The catalog is empty. Press “Add”: a name and a direct installer link.",
        ["Записей: {0}. Папка загрузок: {1}"] = "Entries: {0}. Download folder: {1}",
        ["Не удалось сохранить каталог: {0}"] = "Could not save the catalog: {0}",
        ["Открыт пакет «{0}». Это отдельный список — каталог не меняется."] =
            "Opened pack “{0}”. It is a separate list — the catalog is unchanged.",
        ["Открыт каталог."] = "Catalog opened.",
        ["Название новой категории:"] = "Name of the new category:",
        ["Такая категория уже есть."] = "That category already exists.",
        ["Категория добавлена: {0}"] = "Category added: {0}",
        ["Новое название для «{0}»:"] = "New name for “{0}”:",
        ["Категория «{0}» переименована в «{1}»."] = "Category “{0}” renamed to “{1}”.",
        ["Удалить пустую категорию «{0}»?"] = "Delete the empty category “{0}”?",
        ["Удалить категорию «{0}»?\n\nПрограмм в ней: {1}. Они перейдут в «Без категории»."] =
            "Delete the category “{0}”?\n\nPrograms in it: {1}. They will move to “No category”.",
        ["Категория «{0}» удалена."] = "Category “{0}” deleted.",
        ["Новая категория…"] = "New category…",
        ["Переименовать…"] = "Rename…",
        ["Выше"] = "Move up",
        ["Ниже"] = "Move down",
        ["Экспорт каталога…"] = "Export catalog…",
        ["Импорт каталога…"] = "Import catalog…",
        ["Скачать"] = "Download",
        ["Открыть сайт"] = "Open website",
        ["Скопировать ссылку"] = "Copy link",
        ["Изменить…"] = "Edit…",
        ["Ссылка скопирована: {0}"] = "Link copied: {0}",
        ["Не удалось скопировать: {0}"] = "Could not copy: {0}",
        ["Не удалось открыть сайт: {0}"] = "Could not open the website: {0}",
        ["Экспорт каталога"] = "Export catalog",
        ["Импорт каталога"] = "Import catalog",
        ["Каталог SetupCrate (*.json)|*.json"] = "SetupCrate catalog (*.json)|*.json",
        ["Каталог SetupCrate (*.json)|*.json|Все файлы (*.*)|*.*"] =
            "SetupCrate catalog (*.json)|*.json|All files (*.*)|*.*",
        ["Экспортировано записей: {0} → {1}"] = "Exported entries: {0} → {1}",
        ["Не удалось экспортировать: {0}"] = "Could not export: {0}",
        ["Не удалось прочитать файл: {0}"] = "Could not read the file: {0}",
        ["В выбранном файле нет записей."] = "The selected file contains no entries.",
        ["В файле записей: {0}.\n\n«Да» — заменить текущий каталог.\n«Нет» — добавить к текущему (дубликаты пропущу)."] =
            "Entries in the file: {0}.\n\n“Yes” — replace the current catalog.\n“No” — add to the current one (duplicates will be skipped).",
        ["Импорт завершён. Записей сейчас: {0}."] = "Import finished. Entries now: {0}.",
        ["Сейчас открыт пакет. Вернись в каталог, чтобы добавить программу."] =
            "A pack is open right now. Return to the catalog to add a program.",
        ["Добавлено: {0}"] = "Added: {0}",
        ["Пакет — встроенный список, его записи не редактируются."] =
            "A pack is a built-in list; its entries cannot be edited.",
        ["Сохранено: {0}"] = "Saved: {0}",
        ["Пакет — встроенный список, из него ничего не удаляется."] =
            "A pack is a built-in list; nothing is deleted from it.",
        ["Удалить из каталога: {0} шт.?"] = "Delete from the catalog: {0} item(s)?",
        ["Удалено: {0}"] = "Deleted: {0}",
        ["Это витрина пакета, а не каталог — удалять нечего. Вернись в каталог."] =
            "This is a pack showcase, not the catalog — nothing to delete. Return to the catalog.",
        ["Отметь галочками программы, которые нужно удалить."] = "Tick the programs you want to delete.",
        ["Отметь галочками, что скачать."] = "Tick what to download.",
        ["Список пуст — скачивать нечего."] = "The list is empty — nothing to download.",
        ["Отметь галочками, что устанавливать."] = "Tick what to install.",
        ["Список пуст — проверять нечего."] = "The list is empty — nothing to check.",
        ["Не удалось открыть папку: {0}"] = "Could not open the folder: {0}",
        ["Отмена…"] = "Cancelling…",
        ["Сейчас открыт пакет — для него кнопка ниже, в блоке «Пакеты»."] =
            "A pack is open — use the button below in the “Packs” block.",
        ["Категории"] = "Categories",
        ["Сначала открой пакет в блоке «Пакеты»."] = "Open a pack in the “Packs” block first.",
        ["В этом пакете нет программ."] = "There are no programs in this pack.",
        ["В этой категории нечего отмечать."] = "There is nothing to select in this category.",
        ["{0}: {1} {2}."] = "{0}: {1} {2}.",
        ["отмечено"] = "selected",
        ["снято"] = "cleared",

        // ---------- статусы элементов ----------
        ["скачивание…"] = "downloading…",
        ["скачивание {0}"] = "downloading {0}",
        ["отменено"] = "cancelled",
        ["✓ скачано"] = "✓ downloaded",
        ["✓ подпись ок"] = "✓ signature ok",
        ["⚠ без подписи"] = "⚠ unsigned",
        ["⚠ подпись не ок"] = "⚠ signature issue",
        ["· sha256 не проверен"] = "· sha256 not verified",
        ["· докачано"] = "· resumed",
        ["проверка…"] = "checking…",
        ["✗ файл не скачан — сначала «Скачать»"] = "✗ file not downloaded — press “Download” first",
        ["запуск установщика…"] = "launching installer…",
        ["✓ установщик завершён (код {0})"] = "✓ installer finished (code {0})",

        // ---------- лог ----------
        ["Старт загрузки: {0} шт. → {1}"] = "Download started: {0} file(s) → {1}",
        ["{0}: ошибка — {1}"] = "{0}: error — {1}",
        ["Готово: скачано {0} из {1}"] = "Done: {0} of {1} downloaded",
        [", ошибок {0}"] = ", errors {0}",
        [". Папка: {0}"] = ". Folder: {0}",
        ["Все ссылки отвечают."] = "All links respond.",
        ["Проверено {0}, проблемных: {1}."] = "Checked {0}, problems: {1}.",
        ["{0}: установщик завершён, код {1}"] = "{0}: installer finished, code {1}",
        ["{0}: ошибка запуска — {1}"] = "{0}: launch error — {1}",
        ["Установка завершена."] = "Installation finished.",
        ["Не удалось прочитать список установленных программ: {0}"] =
            "Could not read the list of installed programs: {0}",

        // ---------- диалоги ----------
        ["Название программы *"] = "Program name *",
        ["Официальный сайт (для справки и поиска ссылок)"] = "Official site (reference and link search)",
        ["Прямая ссылка на установщик *"] = "Direct installer link *",
        ["Найти ссылки на странице…"] = "Find links on page…",
        ["Ищу…"] = "Searching…",
        ["Категория"] = "Category",
        ["Заметки (например: ставить в C:\\Program1\\..., версия x64)"] =
            "Notes (e.g. install to C:\\Program1\\..., x64 build)",
        ["Дополнительно (можно оставить пустым)"] = "Advanced (optional)",
        ["Флаги тихой установки"] = "Silent install flags",
        ["Папка установки (подсказка)"] = "Install folder (hint)",
        ["Ожидаемый SHA-256 (необязательно)"] = "Expected SHA-256 (optional)",
        ["Флаги нужны только если хочешь ставить молча — путь тогда будет стандартный. Пусто = обычный запуск установщика, где путь выбираешь сам."] =
            "Flags are only needed for a silent install — the path will then be the default one. Empty = a normal installer run where you choose the path yourself.",
        ["Запись каталога"] = "Catalog entry",
        ["Найденные файлы"] = "Found files",
        ["Выбери ссылку на установщик. Если нужного файла нет — закрой окно и скопируй ссылку с сайта вручную."] =
            "Choose an installer link. If the file is missing, close the window and copy the link from the site manually.",
        ["Выбрать"] = "Choose",
        ["Укажи название программы."] = "Enter the program name.",
        ["Нужна полная ссылка на файл, начиная с https:// (например, https://site.com/app-x64.exe)."] =
            "A full file link starting with https:// is required (e.g. https://site.com/app-x64.exe).",
        ["Сайт должен быть полной ссылкой, начиная с https://"] = "The site must be a full link starting with https://",
        ["Сначала укажи сайт или ссылку, с которой искать файлы."] = "Enter a site or link to search files on first.",
        ["На этой странице не нашлось ссылок .exe/.msi/.zip. Открой сайт вручную и скопируй прямую ссылку."] =
            "No .exe/.msi/.zip links were found on this page. Open the site manually and copy a direct link.",
        ["Не удалось загрузить страницу: {0}"] = "Could not load the page: {0}",
        ["Название не может быть пустым."] = "The name cannot be empty.",

        // ---------- сервисы ----------
        ["Некорректная ссылка: нужен полный http(s)-адрес."] = "Invalid link: a full http(s) address is required.",
        ["по ссылке страница сайта, а не установщик — нужна прямая ссылка на файл"] =
            "the link points to a website page, not an installer — a direct file link is required",
        ["✗ ссылка не открылась"] = "✗ link did not respond",
        ["✗ ссылка не отвечает ({0})"] = "✗ link does not respond ({0})",
        ["⚠ ведёт на страницу сайта, а не на файл"] = "⚠ leads to a website page, not a file",
        ["✓ ссылка работает"] = "✓ link works",
        ["✗ файл не найден"] = "✗ file not found",
        ["✓ подписано: {0}"] = "✓ signed: {0}",
        ["(подпись действительна)"] = "(signature valid)",
        ["нет встроенной подписи (для установщиков это повод насторожиться; у системных файлов Windows подпись каталожная)"] =
            "no embedded signature (for installers this is a warning sign; Windows system files use catalog signatures)",
        ["нет встроенной подписи"] = "no embedded signature",
        ["подпись не распознана"] = "signature not recognized",
        ["провайдер подписи не найден"] = "signature provider not found",
        ["подпись есть, но корневой сертификат не доверенный"] = "signed, but the root certificate is untrusted",
        ["сертификат подписи истёк"] = "the signing certificate has expired",
        ["подпись недействительна (0x{0})"] = "invalid signature (0x{0})",
        ["⚠ проверка подписи не удалась: {0}"] = "⚠ signature check failed: {0}",
        ["Не удалось запустить установщик."] = "Could not start the installer.",
        ["Базовый"] = "Basic",
        ["Игры"] = "Games",
        ["Работа"] = "Work",
    };
}

/// <summary>Разметка в XAML: Text="{loc:Tr 'Отмена'}" — перевод обновляется при смене языка.</summary>
public sealed class TrExtension : MarkupExtension
{
    public string Text { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(Loc.Version))
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            Converter = TrConverter.Instance,
            ConverterParameter = Text
        };

        return binding.ProvideValue(serviceProvider);
    }
}

public sealed class TrConverter : IValueConverter
{
    public static readonly TrConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(parameter as string ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
