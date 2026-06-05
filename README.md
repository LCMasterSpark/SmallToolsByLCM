# LCM Toolbox

![Version](https://img.shields.io/badge/version-3.1.0-68217A?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF%20%2B%20MahApps-68217A?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

**LCM Toolbox** is a Windows desktop toolbox by **LCMasterSpark**. It brings developer utilities, Office/PDF helpers, local batch tools, network diagnostics, translation tools, QR generation, OCR, and a few deliberately silly toys into one VS2022-style WPF app.

> Looking for the executable? Open the repository's **Releases** page and download the `.exe` asset. GitHub's main code page shows source files by design; release binaries live separately.

## What's New in 3.1

- Added a settings center with sound, theme, networking/OCR, task, data, and about sections.
- Added favorites and recent tools for faster navigation.
- Added task history with retry support for batch workflows.
- Added screenshot OCR and manual GitHub release update checks.
- Added translation tools: text translation, file translation, and a realtime OCR translation overlay.
- Cleaned up architecture: catalog providers, handler registry, state services, dynamic parameter builder, and interactive view factory.

## Highlights

- **VS2022-style workbench**: purple dark theme, Activity Bar, searchable tool browser, command bar, editor-like panes, property panel, status bar, subtle sounds, and button feedback.
- **OfficeHelper**: CSV cleaning, Excel/CSV conversion, Word text extraction, Office image extraction, Word replacement, PDF lite tools, and local Office/LibreOffice/WPS conversion helpers.
- **Translation**: configurable providers including LibreTranslate, Azure, DeepL, Google, Baidu, Youdao, OpenAI-compatible APIs, and Ollama.
- **Local-first utilities**: most tools run offline and do not save input text, secrets, OCR images, or output results.
- **Batch workflows**: file encryption, image conversion/compression, MP3 extraction, hashing, Office/PDF processing, pause support, non-overwriting output paths, and failed-item retry.
- **Interactive tools**: QR Code generator, screen pointer overlay, realtime translator overlay, minesweeper, power checker, and time pointer.
- **Online cards**: optional Hitokoto, poem line, weather, and IP information tools use public APIs with short timeouts and source labels.
- **Single-file publishing**: self-contained Windows x64 release builds.

## Download

1. Go to the **Releases** page.
2. Download the latest `LCM-Toolbox-*.exe` asset.
3. Run it on Windows x64.

The release executable is self-contained, so it does not require a separate .NET runtime installation.

## Tool Groups

| Group | Examples |
| --- | --- |
| Encoding & Conversion | Base64, URL tools, HTML entities, Unicode escaping |
| Text Formatting | JSON/XML format and minify, JWT parser, regex tester, text diff |
| Crypto & Hashing | SHA-256, SHA-512, MD5, HMAC, AES-GCM, RSA-OAEP |
| File & Batch Processing | File encryption, MP4 to MP3, image conversion/compression, file hashing |
| OfficeHelper | CSV cleaner, Excel/CSV tools, Word tools, PPT text extraction, PDF tools, local Office conversion |
| Network & API | HTTP request, URL parameters, headers/cookies, ping, port usage, DNS, public IP, curl |
| Generators | UUID, timestamp, password generator, QR Code, screen pointer, screenshot OCR |
| Translation | Text translation, file translation, realtime OCR translation overlay |
| Fun Lab | random picker, shuffle, dice, text toys, fake logs, excuses, minesweeper, online cards |

## OfficeHelper

OfficeHelper is split into built-in tools and optional local-engine tools:

- **Open XML tools** handle `.docx`, `.xlsx`, and `.pptx` without requiring Microsoft Office.
- **PDF Lite tools** can inspect PDFs, extract text/images, and export editable text-only `.docx` files.
- **Local advanced conversion** can use Microsoft Office COM, LibreOffice headless mode, or experimental WPS automation when those engines are installed on the user's machine.

The toolbox does not upload Office/PDF files for conversion.

## Translation and OCR

Translation settings are stored locally in `app-state.json`. API keys are saved in plain text because this is a local desktop utility, so avoid storing sensitive production keys on shared machines.

Screenshot OCR can use OCR.space when networking is enabled and an API key is configured. Windows OCR is used as a local fallback when available.

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
  Models/                 Tool definitions, requests, results, and shared models
  Services/
    Catalog/              Tool metadata providers by feature group
    ToolHandlers/         Handler registry and feature-specific tool implementations
    State/                app-state.json services and preferences
    Ocr/                  OCR services
    Translation/          Translation provider and file translation services
  ViewModels/
    Shell/                Main shell ViewModel partials by responsibility
  Views/
    Controls/             Shared UI control builders and dialog services
    Shell/                Shell factories and host abstractions
    Generation/           Interactive generator controls
    FunLab/               Fun Lab interactive controls
    Translation/          Realtime translation controls and overlay
  MainWindow.xaml         VS2022-style shell layout
  MainWindow.xaml.cs      Window lifecycle, theme, dialogs, and interactive host wiring

小工具集合.Tests/
  xUnit unit and lightweight integration tests
```

## Privacy

LCM Toolbox does not save tool input text, OCR images, translation content, or output results. It stores local preferences, task metadata, screen pointer settings, translation settings, and minesweeper stats in `app-state.json`.

Online tools send the current query to their corresponding public API. Keep networking disabled when content should not leave the machine.

## Third-Party Notices

- MahApps.Metro - MIT
- ControlzEx - MIT
- XamlAnimatedGif - MIT
- QRCoder - MIT
- CsvHelper - MS-PL or Apache-2.0
- DocumentFormat.OpenXml - MIT
- PdfPig / UglyToad.PdfPig - Apache-2.0
- SixLabors.ImageSharp - Six Labors Split License
- Kenney UI Audio - CC0

The bundled Marisa GIF is a user-provided decorative resource. Confirm its source license before public redistribution outside this repository's current release packaging.

## License

MIT License. See [LICENSE](LICENSE).

## Author

Made by **LCMasterSpark**.
