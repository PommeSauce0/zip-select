# ZIP Select

[Français](../README.md)

Extract selected file types from multiple ZIP archives, with a graphical interface for Windows and Linux x64.

**Windows has been tested. Linux is experimental: its package builds, but has not been tested on a Linux desktop.** macOS is not supported.

![English interface](interface-english.png)

## Get started

Download a package from the repository’s GitHub Releases and unpack it. On Windows, open `ZIP-Select.exe`. On Linux, run `./ZIP-Select`, using `chmod +x ZIP-Select` if needed. The Linux build targets glibc distributions with X11 or XWayland, Fontconfig and the usual desktop graphics libraries. Alpine/musl is not targeted. Packages include .NET; no SDK is needed to run them.

Choose **Langue → English** to switch to English. The choice is saved and takes effect immediately, without restarting. Language changes are disabled during extraction. Previous log entries keep their original language; operating system error details and native dialogs may use the system language.

1. Select the folder containing your ZIP archives.
2. Select an extraction folder.
3. Enter extensions such as `.mp3, .pdf`, or `*` for all files.
4. Adjust **Options**, then click **Extract**.

Options control recursive ZIP search, inclusion of folders inside archives, keeping or flattening their structure, a separate destination folder for each archive, and automatic conflict renaming. The example illustrates your settings; it is not a preview of the actual archives.

Archives are kept, and existing files are never overwritten. A matching destination and file size cause a file to be skipped, **without comparing contents**. Stopping keeps completed files; starting again resumes using these rules. Folders without selected files are not created.

## Development

Install the .NET 10 SDK. From the repository root:

```sh
dotnet restore tests/ZipSelect.Tests.csproj --configfile NuGet.Config
dotnet run --project tests/ZipSelect.Tests.csproj --no-restore
dotnet run --project src/Desktop/ZipSelect.Desktop.csproj
```

With PowerShell, `./scripts/Build.ps1 -Action Test` runs the tests. `./scripts/Package.ps1 -Runtime win-x64` or `-Runtime linux-x64` creates packages under `dist/packages/`.

`src/Core/` contains extraction and the shared `Translations.json` catalog; `src/Desktop/` contains the Avalonia UI. Tests cover extraction in both languages, live language switching, settings persistence and the headless UI. Native desktop integration still needs manual testing on each platform.

Settings remain in `ZipSelect-Avalonia/settings.json` under .NET’s `LocalApplicationData` folder for compatibility with version 1.0.0. Build outputs, local tools and backups are excluded from Git.

The [project logo](../assets/branding/logo.png) was **generated with AI using OpenAI image_gen**. Its [prompt and provenance](../assets/branding/Generation.md) are preserved, along with attribution in the source. The logo is displayed in the application and embedded as its icon. `logo.ico` is a multi-resolution conversion of the original PNG for Windows.
