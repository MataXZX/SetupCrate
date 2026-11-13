# SetupCrate

A small Windows app for keeping a personal catalog of installer links and downloading them in one click — your own lightweight "Ninite", with three themes (Light, Dark, Glass) and full control over install paths.

## Features

- Portable catalog in `catalog.json` next to the exe — easy to move, sync or back up.
- Add entries: name, website, direct download URL, category, notes.
- "Find links on page" — scans a site for `.exe/.msi/.zip` candidates and lets you pick one.
- Download selected / all, in parallel, with progress and resume (`HTTP Range`).
- Authenticode signature check after download (plus optional SHA-256).
- "Check links" — HEAD-check the whole catalog, so dead links are visible at a glance.
- "Run installers" sequentially, with optional silent-install flags.
- "Installed" detection via the Windows Uninstall registry.
- Import / export the catalog.
- Categories: create, rename, delete, drag-and-drop reorder.
- Built-in preset packs (Basic / Games / Work) kept fully separate from your own catalog.
- Russian and English interface, chosen automatically from the Windows display language (switchable with the small `RU`/`EN` button in the status bar).
- Three themes: **Light**, **Dark**, **Glass** (DWM acrylic blur where supported, falls back to a translucent gradient everywhere).

## Screenshots

| Light | Dark | Glass |
|-------|------|-------|
| ![Light](docs/light.png) | ![Dark](docs/dark.png) | ![Glass](docs/glass.png) |

## Build

Requires the .NET 10 SDK.

```
dotnet publish SetupCrate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

or simply run `build.cmd`.

Output: `bin\Release\net10.0-windows\win-x64\publish\SetupCrate.exe` — a self-contained single file, no runtime required on the target machine.

## Usage

1. Run `SetupCrate.exe`.
2. Add programs, tick the ones you want, press **Скачать отмеченные** ("Download selected").
3. Installers are saved to `downloads\` next to the app; run them and choose your install path.

Everything stays local. `catalog.json` is portable.

## Windows SmartScreen

The release binary is not code-signed yet, so Windows may show **"Windows protected your PC"** on the first launch. To run it:

1. Click **More info** → **Run anyway**.
2. Optionally check the file hash against the SHA-256 published in the release notes:

```
certutil -hashfile SetupCrate.exe SHA256
```

Why this happens: SmartScreen trusts only files signed with a recognized code-signing certificate (and ones with enough download reputation). Signing future builds with a trusted certificate is the only way to remove the warning completely.

## License

[MIT](LICENSE)
