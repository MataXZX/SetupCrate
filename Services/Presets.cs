namespace SetupCrate.Services;

public sealed record PresetApp(string Name, string Url, string Site, string Notes = "");

public sealed record Preset(string Name, IReadOnlyList<PresetApp> Apps);

public static class Presets
{
    private static readonly Preset Base = new("Базовый", new[]
    {
        new PresetApp("Firefox",
            "https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=ru",
            "https://www.mozilla.org/ru/firefox/",
            "Браузер, открытый код (MPL)"),
        new PresetApp("7-Zip",
            "https://www.7-zip.org/a/7z2606-x64.exe",
            "https://www.7-zip.org/",
            "Архиватор, открытый код (LGPL)"),
        new PresetApp("VLC",
            "https://get.videolan.org/vlc/last/win64/vlc-3.0.24-win64.exe",
            "https://www.videolan.org/vlc/",
            "Медиаплеер, открытый код (GPL)"),
        new PresetApp("Notepad++",
            "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.9.9.1/npp.8.9.9.1.Installer.x64.exe",
            "https://notepad-plus-plus.org/",
            "Текстовый редактор, открытый код (GPL)"),
        new PresetApp("Everything",
            "https://www.voidtools.com/Everything-1.4.1.1032.x64-Setup.exe",
            "https://www.voidtools.com/",
            "Мгновенный поиск файлов. Бесплатно, но не open source"),
        new PresetApp("qBittorrent",
            "https://sourceforge.net/projects/qbittorrent/files/latest/download",
            "https://www.qbittorrent.org/",
            "Торрент-клиент, открытый код (GPL)")
    });

    private static readonly Preset Games = new("Игры", new[]
    {
        new PresetApp("Steam",
            "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe",
            "https://store.steampowered.com/",
            "Магазин и лаунчер игр (закрытый код)"),
        new PresetApp("Heroic Games Launcher",
            "https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/releases/download/v2.22.4/Heroic-2.22.4-Setup-x64.exe",
            "https://heroicgameslauncher.com/",
            "Epic/GOG-лаунчер, открытый код (GPL)"),
        new PresetApp("Playnite",
            "https://github.com/JosefNemec/Playnite/releases/download/10.64/Playnite1064.exe",
            "https://playnite.link/",
            "Библиотека всех игр в одном месте, открытый код (MIT)"),
        new PresetApp("Prism Launcher",
            "https://github.com/PrismLauncher/PrismLauncher/releases/download/11.1.1/PrismLauncher-Windows-MSVC-Setup-11.1.1.exe",
            "https://prismlauncher.org/",
            "Лаунчер Minecraft, открытый код (GPL)")
    });

    private static readonly Preset Work = new("Работа", new[]
    {
        new PresetApp("LibreOffice",
            "https://download.documentfoundation.org/libreoffice/stable/26.8.3/win/x86_64/LibreOffice_26.8.3_Win_x86-64.msi",
            "https://ru.libreoffice.org/",
            "Офисный пакет, открытый код (MPL)"),
        new PresetApp("Thunderbird",
            "https://download.mozilla.org/?product=thunderbird-latest-ssl&os=win64&lang=ru",
            "https://www.thunderbird.net/ru/",
            "Почтовый клиент, открытый код (MPL)"),
        new PresetApp("Visual Studio Code",
            "https://update.code.visualstudio.com/latest/win32-x64-user/stable",
            "https://code.visualstudio.com/",
            "Редактор кода, открытый код (MIT)"),
        new PresetApp("Git for Windows",
            "https://github.com/git-for-windows/git/releases/download/v2.56.1.windows.1/Git-2.56.1-64-bit.exe",
            "https://git-scm.com/download/win",
            "Система контроля версий, открытый код (GPL)"),
        new PresetApp("KeePassXC",
            "https://github.com/keepassxreboot/keepassxc/releases/download/2.8.0/KeePassXC-2.8.0-x64.msi",
            "https://keepassxc.org/",
            "Менеджер паролей, открытый код (GPL)"),
        new PresetApp("SumatraPDF",
            "https://www.sumatrapdfreader.org/dl/rel/3.6.1/SumatraPDF-3.6.1-64-install.exe",
            "https://www.sumatrapdfreader.org/",
            "Лёгкий просмотрщик PDF, открытый код (AGPL)")
    });

    public static IReadOnlyList<Preset> All { get; } = new[] { Base, Games, Work };
}
