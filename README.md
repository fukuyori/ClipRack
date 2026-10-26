# ClipRack

ClipRack is a Windows WPF application for viewing, editing, and transforming text from Windows clipboard history.

ClipRack is distributed under the MIT License. See [LICENSE](LICENSE) for details. Third-party package licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Requirements

- Windows 10 version 1809 or later
- .NET 10 SDK
- Windows clipboard history enabled

## Run

```powershell
dotnet run --project .\Clippy.csproj
```

The app starts in the system tray. Open it from the tray icon or use the configured activation shortcut. The interface follows the Windows theme and display language by default; both can be overridden in Settings. English and Japanese are supported.

## Features

- View text items from Windows clipboard history
- Extract plain text from formatted text, HTML, and XML
- Convert line endings to CRLF, LF, or CR
- Automatically normalize plain-text-only clipboard content to CRLF; formatted, image, and unsupported clipboard data is left untouched
- Collapse consecutive blank lines and replace text using literal strings or .NET regular expressions
- Create, save, and run ordered text macros, including from a history item's context menu
- Translate text with Ollama through `/api/generate`
- Configure light/dark appearance, interface language, Windows sign-in startup, activation shortcut, and window placement
- Keep the original history item; macro results are added as new clipboard items

The tray icon uses [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for its license information.

Settings and macros are stored as JSON under `%LOCALAPPDATA%\Clippy` for compatibility with earlier versions. Clipboard history text is not stored in ClipRack's own data directory.

## Build and package

Run these scripts from PowerShell. The version is read from `Clippy.csproj`.

```powershell
.\scripts\build-release.ps1
.\scripts\build-installer.ps1
```

To sign the app executable, installer, and uninstaller, configure `CODESIGN_CERT` and use `-Sign`. For a PFX/P12 certificate, optionally set `CODESIGN_PASSWORD`.

```powershell
.\scripts\build-installer.ps1 -Sign
```

Outputs are written to `artifacts\publish\win-x64` and `installer_output`. See [docs/version-update-checklist.md](docs/version-update-checklist.md) when changing the version.

## Limitations

- Image processing is planned for a later stage.
- Translation is a standalone feature and is not available as a macro step.
- HTML/XML conversion may not preserve semantic spacing or line breaks in complex documents.
- Confirm that converted results appear in Windows clipboard history from the History tab.
- Automatic newline normalization occurs after a clipboard update, so both the original and normalized text can appear in Windows history.
- Windows clipboard-history behavior and Ollama connectivity have not yet been manually verified on a target system.

See [README.ja.md](README.ja.md) for the Japanese version.
