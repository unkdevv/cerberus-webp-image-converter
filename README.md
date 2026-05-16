# Cerberus WebP Image Converter

Cerberus WebP Image Converter is a Windows desktop app for fast local image conversion. It works as a batch image converter for WebP, AVIF, JPG, PNG, and ICO, with automatic format detection, image previews, quality control, and output-size estimation before conversion.

The default output format is **WebP**, making the app useful for optimizing website assets, UI images, icons, screenshots, and local image collections without uploading files to an online converter.

## Why Cerberus?

Most image conversion tools either focus on one file at a time or require an online upload. Cerberus is built for local batch image conversion on Windows: add images, preview them, tune quality, estimate size reduction, and convert everything into a predictable output folder.

## Features

- Convert images locally, without uploading files to an online image converter.
- Automatically detect image format, dimensions, and original size.
- Default conversion to WebP for smaller web-ready images.
- Output support for:
  - WebP
  - AVIF
  - JPG
  - PNG
  - ICO
- Quality control with a slider.
- Automatic output-size estimation when the output format or quality changes.
- Preview thumbnails in the image queue.
- Remove individual images from the queue.
- Remove selected images or clear the full queue.
- Drag and drop individual images or full folders into the app.
- Show original size, estimated output size, and estimated reduction.
- Create a numbered subfolder when converting multiple images.
- Persist temporary user preferences for quality and custom output folder.
- Native Windows title bar with dark mode support.

## Screens and Workflow

The main screen is split into:

- **Image queue**: shows preview, file name, input format, size, resolution, estimated output, reduction, progress, status, and a per-row remove action.
- **Configuration panel**: lets the user choose the output format, quality, and output folder.
- **Conversion action**: converts all queued images using the current settings.

When multiple images are converted, the app asks whether to save them in a separate folder. If confirmed, it creates numeric folders inside the default Cerberus folder:

```text
Desktop\cerberus\1
Desktop\cerberus\2
Desktop\cerberus\3
```

The app always reuses the existing `Desktop\cerberus` folder if it already exists.

## Output Paths

By default, converted images are saved in:

```text
%USERPROFILE%\Desktop\cerberus
```

If the user selects a custom output folder, that custom path is remembered temporarily and restored the next time the app starts.

Temporary preferences are stored at:

```text
%TEMP%\CerberusConverter\preferences.json
```

The preferences file stores:

- custom output folder, only when the user selects one
- image quality

The default folder itself is not stored as a custom preference.

## Image Format Notes

### WebP

WebP is the default because it usually gives a strong balance between quality and file size.

### AVIF

AVIF is available as an output option. Support depends on the native image encoder available in the runtime. If AVIF encoding is not available on a given machine, the app will show an error for that conversion.

### JPG

JPG output uses a white background when the source image has transparency, because JPG does not support alpha transparency.

### PNG

PNG preserves transparency and is useful for lossless or UI-oriented assets.

### ICO

ICO output is generated as a valid icon container with an internal PNG image. If the source image is larger than 256 pixels on its largest side, it is resized proportionally for icon compatibility.

## Requirements

- Windows 10 or Windows 11.
- .NET 10 SDK.
- Visual Studio 2022 or VS Code with the C# tooling.

The project targets:

```xml
net10.0-windows
```

The app uses WPF, so it is Windows-only.

## Build With VS Code

1. Install the .NET 10 SDK.
2. Install the **C# Dev Kit** extension in VS Code.
3. Open the repository folder:

```powershell
code C:\path\to\cerberus
```

4. Restore packages:

```powershell
dotnet restore .\CerberusConverter.sln
```

5. Build:

```powershell
dotnet build .\CerberusConverter.sln
```

6. Run:

```powershell
dotnet run --project .\src\CerberusConverter\CerberusConverter.csproj
```

## Build With Visual Studio

1. Open `CerberusConverter.sln`.
2. Make sure the selected configuration is `Debug` or `Release`.
3. Restore NuGet packages if Visual Studio does not do it automatically.
4. Build the solution with:

```text
Build > Build Solution
```

5. Run with:

```text
Debug > Start Debugging
```

or:

```text
Debug > Start Without Debugging
```

## Publish a Single-File EXE

The repository includes a Visual Studio publish profile for generating a single self-contained Windows x64 executable.

Profile path:

```text
src\CerberusConverter\Properties\PublishProfiles\SingleFileWinX64.pubxml
```

This profile enables:

- `Release` configuration
- `win-x64` runtime
- self-contained publish
- single-file executable
- native library extraction support
- compression inside the single-file bundle

### Publish from CLI

Use the included publish profile:

```powershell
dotnet publish .\src\CerberusConverter\CerberusConverter.csproj -p:PublishProfile=SingleFileWinX64
```

The single-file executable will be generated under:

```text
bin\publish
```

The output executable is:

```text
bin\publish\Cerberus.exe
```

You can also publish manually with:

```powershell
dotnet publish .\src\CerberusConverter\CerberusConverter.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

### Publish from Visual Studio

The `Debug` / `Release` dropdown controls normal build configuration. Single-file publishing is handled through a publish profile.

In Visual Studio:

1. Right-click the `CerberusConverter` project.
2. Select `Publish`.
3. Choose or import the `SingleFileWinX64` profile.
4. Click `Publish`.

The generated app is intended to run without requiring the user to install the .NET runtime separately.

## Project Structure

```text
CerberusConverter.sln
src/
  CerberusConverter/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    CerberusConverter.csproj
    Data/
      icone.ico
    Models/
      ConversionItem.cs
      ImageConversionOptions.cs
      MediaMetadata.cs
    Services/
      FormatHelpers.cs
      ImageConversionService.cs
```

## Important Files

- `MainWindow.xaml`: main WPF interface.
- `MainWindow.xaml.cs`: UI behavior, queue handling, preference persistence, and conversion workflow.
- `ImageConversionService.cs`: image metadata, estimation, conversion, thumbnail generation, and ICO generation.
- `FormatHelpers.cs`: output filename handling and file-size formatting.
- `ConversionItem.cs`: queue item model used by the UI.
- `Data\icone.ico`: application icon.

## Troubleshooting

### `dotnet` is not recognized

Install the .NET 10 SDK and restart the terminal or IDE.

Check installation with:

```powershell
dotnet --info
```

### Old UI or removed controls still appear

Clean the project:

```powershell
dotnet clean .\CerberusConverter.sln
dotnet build .\CerberusConverter.sln
```

If the executable is currently running, close it before cleaning or rebuilding.

### Build cannot delete `.exe` or `.dll`

The app is probably still running. Close Cerberus Converter or stop the process from Task Manager, then build again.

### AVIF conversion fails

AVIF encoding depends on native runtime support. If AVIF fails on a machine, use WebP, PNG, JPG, or ICO.

### Some animated images convert as one frame

Animated formats such as GIF may be treated as a single decoded frame. Cerberus Converter is currently focused on static image conversion.

## License

Add your preferred license before publishing publicly. Common choices are MIT, Apache-2.0, or GPL-3.0.
