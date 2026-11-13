namespace SetupCrate.Services;

public sealed record PresetApp(string Name, string Url, string Site, string Notes = "");

public sealed record Preset(string Name, IReadOnlyList<PresetApp> Apps)
{
    public string DisplayName => Loc.T(Name);
}

public static class Presets
{
    private static readonly Preset Base = new("Базовый", new[]
    {
        new PresetApp("Firefox",
            "https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=ru",
            "https://www.mozilla.org/ru/firefox/",
            "Быстрый браузер с открытым кодом"),
        new PresetApp("7-Zip",
            "https://www.7-zip.org/a/7z2606-x64.exe",
            "https://www.7-zip.org/",
            "Архиватор: ZIP, 7z и другие форматы"),
        new PresetApp("VLC",
            "https://get.videolan.org/vlc/last/win64/vlc-3.0.24-win64.exe",
            "https://www.videolan.org/vlc/",
            "Плеер, открывает почти любое видео"),
        new PresetApp("Notepad++",
            "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.9.9.1/npp.8.9.9.1.Installer.x64.exe",
            "https://notepad-plus-plus.org/",
            "Лёгкий редактор текста и кода"),
        new PresetApp("qBittorrent",
            "https://sourceforge.net/projects/qbittorrent/files/latest/download",
            "https://www.qbittorrent.org/",
            "Торрент-клиент без рекламы"),
        new PresetApp("Telegram Desktop",
            "https://telegram.org/dl/desktop/win64",
            "https://desktop.telegram.org/",
            "Мессенджер с облачными чатами")
    });

    private static readonly Preset Games = new("Игры", new[]
    {
        new PresetApp("Steam",
            "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe",
            "https://store.steampowered.com/",
            "Крупнейший магазин и лаунчер игр"),
        new PresetApp("Ubisoft Connect",
            "https://static3.cdn.ubi.com/orbit/launcher_installer/UbisoftConnectInstaller.exe",
            "https://www.ubisoft.com/en-gb/ubisoft-connect",
            "Лаунчер и магазин Ubisoft"),
        new PresetApp("EA app",
            "https://origin-a.akamaihd.net/EA-Desktop-Client-Download/installer-releases/EAappInstaller.exe",
            "https://www.ea.com/ea-app",
            "Лаунчер и магазин EA"),
        new PresetApp("Playnite",
            "https://github.com/JosefNemec/Playnite/releases/download/10.64/Playnite1064.exe",
            "https://playnite.link/",
            "Все игры из разных лаунчеров в одном месте"),
        new PresetApp("Prism Launcher",
            "https://github.com/PrismLauncher/PrismLauncher/releases/download/11.1.1/PrismLauncher-Windows-MSVC-Setup-11.1.1.exe",
            "https://prismlauncher.org/",
            "Лаунчер Minecraft с модами"),
        new PresetApp("Discord",
            "https://discord.com/api/download?platform=win",
            "https://discord.com/",
            "Голосовой чат и сообщества")
    });

    private static readonly Preset Work = new("Работа", new[]
    {
        new PresetApp("LibreOffice",
            "https://download.documentfoundation.org/libreoffice/stable/26.8.3/win/x86_64/LibreOffice_26.8.3_Win_x86-64.msi",
            "https://ru.libreoffice.org/",
            "Офисный пакет: документы, таблицы, презентации"),
        new PresetApp("Thunderbird",
            "https://download.mozilla.org/?product=thunderbird-latest-ssl&os=win64&lang=ru",
            "https://www.thunderbird.net/ru/",
            "Почтовый клиент"),
        new PresetApp("Visual Studio Code",
            "https://update.code.visualstudio.com/latest/win32-x64-user/stable",
            "https://code.visualstudio.com/",
            "Редактор кода"),
        new PresetApp("Git for Windows",
            "https://github.com/git-for-windows/git/releases/download/v2.56.1.windows.1/Git-2.56.1-64-bit.exe",
            "https://git-scm.com/download/win",
            "Система контроля версий"),
        new PresetApp("KeePassXC",
            "https://github.com/keepassxreboot/keepassxc/releases/download/2.8.0/KeePassXC-2.8.0-x64.msi",
            "https://keepassxc.org/",
            "Менеджер паролей, всё хранится локально"),
        new PresetApp("ArtCraft",
            "https://github.com/storytold/craft-launcher/releases/download/v0.19.2/artcraft-launcher-0.19.2-windows-x64.msi",
            "https://getartcraft.com/",
            "Открытые инструменты для творчества")
    });

    public static IReadOnlyList<Preset> All { get; } = new[] { Base, Games, Work };
}
