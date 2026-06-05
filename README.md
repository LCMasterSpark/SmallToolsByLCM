# LCM Toolbox

![Version](https://img.shields.io/badge/version-3.0-007ACC?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF%20%2B%20MahApps-5C2D91?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

**LCM Toolbox** is a Windows desktop toolbox by **LCMasterSpark**. It bundles everyday developer utilities, file batch tools, network diagnostics, QR generation, fun widgets, and Office/PDF helpers into one VS2022-style WPF app.

> Looking for the executable? Open the repository's **Releases** page and download the `.exe` asset. GitHub's main code page shows source files by design; release binaries live separately.

## Highlights

- **VS2022-inspired workbench**: dark Activity Bar, searchable tool browser, command bar, editor-like input/output panes, property panel, and status bar.
- **OfficeHelper 3.0**: CSV cleaning, Excel/CSV conversion, Word text extraction, Office image extraction, Word replacement, PDF lite tools, and local Office/LibreOffice/WPS conversion helpers.
- **Local-first utilities**: most tools run offline and do not store input text, secrets, or output history.
- **Batch-friendly workflows**: file encryption, image conversion/compression, MP3 extraction, hashing, Office/PDF processing, pause support, and non-overwriting output paths.
- **Interactive tools**: screen pointer overlay, QR Code generator, minesweeper, power checker, and time pointer.
- **Online cards**: optional Hitokoto, poem line, weather, and IP information tools use public APIs with short timeouts and clear source labels.
- **Single-file publishing**: supports self-contained Windows x64 builds.

## Download

1. Go to **Releases**.
2. Download `LCM-Toolbox-3.0.exe` or the latest `.exe` asset.
3. Run it on Windows x64.

The app is self-contained, so the release executable does not require a separate .NET runtime installation.

## Tool Groups

| Group | Examples |
| --- | --- |
| Encoding & Conversion | Base64, URL tools, HTML entities, Unicode escaping |
| Text Formatting | JSON/XML format and minify, JWT parser, regex tester, text diff |
| Crypto & Hashing | SHA-256, SHA-512, MD5, HMAC, AES-GCM, RSA-OAEP |
| File & Batch Processing | File encryption, MP4 to MP3, image conversion/compression, file hashing |
| OfficeHelper | CSV cleaner, Excel merge, Excel/CSV tools, Word tools, PDF tools, local Office conversion |
| Network & API | HTTP request, URL parameters, headers/cookies, ping, port usage, DNS, public IP, curl |
| Generators | UUID, timestamp, password generator, QR Code, screen pointer |
| Fun Lab | random picker, shuffle, dice, text toys, fake logs, excuses, minesweeper, online cards |

## OfficeHelper 3.0

OfficeHelper is split into lightweight built-in tools and optional local-engine tools:

- **Built-in Open XML tools** handle `.docx`, `.xlsx`, and `.pptx` without requiring Microsoft Office.
- **PDF Lite tools** can inspect PDFs, extract text/images, and export editable text-only `.docx` files.
- **Local advanced conversion** can use Microsoft Office COM, LibreOffice headless mode, or experimental WPS automation when those engines are installed on the user's machine.

The toolbox does not upload Office/PDF files for conversion.

## Build

Requirements:

- Windows
- .NET SDK 10

Build:

```powershell
dotnet build 小工具集合.slnx --no-restore
```

Test:

```powershell
dotnet test 小工具集合.slnx --no-restore
```

Publish a self-contained Windows x64 single-file build:

```powershell
dotnet publish 小工具集合\小工具集合.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

## Repository Layout

```text
小工具集合/
  Models/                 Tool definitions, requests, results, and state models
  Services/
    ToolCatalog.cs        Tool metadata and parameter definitions
    ToolProcessor.cs      Tool dispatch entry point
    ToolProcessors/       Partial processors grouped by feature area
  ViewModels/             Main window state, search, commands, and preferences
  Views/
    Generation/           Interactive generator controls
    FunLab/               Fun Lab interactive controls
  MainWindow.xaml         VS2022-style shell
  MainWindow.xaml.cs      Dynamic parameter controls and interactive host wiring

小工具集合.Tests/
  xUnit unit and lightweight integration tests
```

## Privacy

LCM Toolbox does not save input text, API keys, or output history. It only stores local app preferences, screen pointer settings, and minesweeper player stats in the app state file.

Online tools send the current query to their corresponding public API. Keep tools in local mode when the content should not leave the machine.

## License

MIT License. See [LICENSE](LICENSE).

## Author

Made by **LCMasterSpark**.
