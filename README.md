# Video to Animation Tool

Windows local desktop tool for turning videos or still images into animation-ready frame workflows, with local preview, cleanup, and export utilities.

## Features

- Import one video at a time: `mp4`, `mov`, `webm`, or `avi`.
- Load a single image for background or watermark cleanup.
- Choose subject name, motion name, output folder, start/end seconds, export FPS, and image format.
- Export frames with predictable names such as `Hero_Run_0001.png`.
- Preview exported or existing frame folders as animation.
- Adjust preview FPS, loop playback, and step through frames.
- Remove frames from playback and restore them with drag/drop frame lists.
- Remove simple keyed backgrounds with Auto Color Key or Smart Matte.
- Save and reuse background-removal presets.
- Remove selected watermark regions with a preview lasso.
- Export active playback frames as a sprite sheet.
- View FFmpeg log output inside the app.

OpenCV has been removed from this project. External tools should be integrated through small adapters rather than being vendored into the repository.

## Requirements

- Windows with .NET Framework/WPF available.
- Windows PowerShell 5 or newer for build scripts.
- `ffmpeg.exe`.

Place FFmpeg in one of these locations:

- `build/tools/ffmpeg/ffmpeg.exe`
- `src/VideoToAnimationTool/tools/ffmpeg/ffmpeg.exe` for the PowerShell fallback app
- anywhere on your system `PATH`

## Build C# WPF App

This project can build without the .NET SDK by using the Windows .NET Framework C# compiler:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-wpf.ps1
```

The compiled app is written to both:

```text
build\VideoToAnimationTool.exe
VideoToAnimationTool.exe
```

## Run Compiled App

```powershell
.\VideoToAnimationTool.exe
```

## Code Structure

```text
src/VideoToAnimationTool.Core/
  Core file/path utilities, FFmpeg arguments, image cleanup, sprite sheet export,
  validation, preview coordinate mapping, and reusable external tool runner.

src/VideoToAnimationTool.Desktop/
  Desktop-specific models used by the WPF shell.

VideoToAnimationToolDesktop.cs
  Current WPF desktop shell and Studio Workbench UI composition.

scripts/
  Build and test scripts that compile the modular source tree with the local
  .NET Framework C# compiler.
```

When adding a new open-source tool integration, keep process launching, path setup, and result parsing out of `VideoToAnimationToolDesktop.cs`. Add a small adapter under `src/VideoToAnimationTool.Core/ExternalTools` or a dedicated feature folder, then call that adapter from the desktop shell.

## PowerShell Fallback

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\VideoToAnimationTool\VideoToAnimationTool.ps1
```

Both versions open as local desktop windows. They do not upload videos or frames.

## Basic Workflow

1. Click `Browse` in Source Video and choose a video.
2. Enter subject and motion names.
3. Choose an output folder.
4. Set start seconds, end seconds, export FPS, and image format.
5. Click `Export Frames`.
6. Use the preview panel to play the exported sequence.
7. Use `Frame Folder > Load` to preview an existing sequence folder without exporting again.

## Current Limits

- Windows-only.
- One source video per export.
- No GIF or video re-export yet.
- No interpolation or upscaling integrations yet.
- FFmpeg must be provided separately.

## Tests

Run C# core behavior tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-csharp-core.ps1
```

Run PowerShell core behavior tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Core.Tests.ps1
```
