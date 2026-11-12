using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SetupCrate.Models;

public sealed class CatalogItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _site = "";
    private string _url = "";
    private string _category = "Без категории";
    private string _notes = "";
    private string _silentArgs = "";
    private string _targetDir = "";
    private string _sha256 = "";
    private bool _isSelected;
    private string _status = "";
    private long? _size;
    private string? _installedName;
    private string? _installedVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value)) OnPropertyChanged(nameof(Letter)); }
    }

    public string Site { get => _site; set => Set(ref _site, value); }
    public string Url { get => _url; set => Set(ref _url, value); }

    public string Category
    {
        get => _category;
        set { if (Set(ref _category, value)) OnPropertyChanged(nameof(MetaText)); }
    }

    public string Notes { get => _notes; set => Set(ref _notes, value); }

    /// <summary>Флаги тихой установки, например /S или /VERYSILENT /NORESTART. Пусто = обычный запуск.</summary>
    public string SilentArgs { get => _silentArgs; set => Set(ref _silentArgs, value); }

    /// <summary>Куда ставить (подсказка), например C:\Program1\7-Zip. Не влияет на скачивание.</summary>
    public string TargetDir { get => _targetDir; set => Set(ref _targetDir, value); }

    /// <summary>Ожидаемый SHA-256 файла (необязательно).</summary>
    public string Sha256 { get => _sha256; set => Set(ref _sha256, value); }

    public DateTime? LastDownloadUtc { get; set; }

    /// <summary>Имя скачанного файла в папке downloads (заполняется после загрузки).</summary>
    public string? FileName { get; set; }

    [JsonIgnore] public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    [JsonIgnore]
    public string StatusText
    {
        get => _status;
        set
        {
            if (Set(ref _status, value)) OnPropertyChanged(nameof(StatusKind));
        }
    }

    [JsonIgnore]
    public string StatusKind => string.IsNullOrWhiteSpace(_status) ? "none"
        : _status.StartsWith("✓") ? "ok"
        : _status.StartsWith("⚠") ? "warn"
        : _status.StartsWith("✗") ? "error"
        : "busy";

    [JsonIgnore] public string SizeText => _size is > 0 ? FormatBytes(_size.Value) : "";

    [JsonIgnore]
    public string MetaText => string.Join("  ·  ",
        new[] { Category, SizeText }.Where(s => !string.IsNullOrWhiteSpace(s)));

    [JsonIgnore]
    public string Letter => string.IsNullOrWhiteSpace(_name) ? "?" : _name.Trim()[..1].ToUpperInvariant();

    [JsonIgnore]
    public long? Size
    {
        get => _size;
        set
        {
            if (Set(ref _size, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(MetaText));
            }
        }
    }

    [JsonIgnore]
    public string? InstalledName
    {
        get => _installedName;
        set
        {
            if (Set(ref _installedName, value))
            {
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(InstalledText));
            }
        }
    }

    [JsonIgnore]
    public string? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (Set(ref _installedVersion, value)) OnPropertyChanged(nameof(InstalledText));
        }
    }

    [JsonIgnore] public bool IsInstalled => !string.IsNullOrEmpty(InstalledName);

    [JsonIgnore]
    public string InstalledText => IsInstalled ? ("✓ " + (InstalledVersion ?? "")).Trim() : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(prop);
        return true;
    }

    private void OnPropertyChanged(string? prop) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1073741824.0:0.0} ГБ",
        >= 1_048_576 => $"{bytes / 1048576.0:0} МБ",
        >= 1024 => $"{bytes / 1024.0:0} КБ",
        _ => $"{bytes} Б"
    };
}
