# AI Character Sequence Frame Tool

Windows local desktop tool for turning AI-generated character action videos into ordered sequence frames, then previewing those frames as a looping animation.

## Features

- Import one video at a time: `mp4`, `mov`, `webm`, or `avi`.
- Choose character name, action name, output folder, start/end seconds, export FPS, and image format.
- Export frames with predictable names such as `Hero_Run_0001.png`.
- Preview exported or existing frame folders as animation.
- Adjust preview FPS, loop playback, and step through frames.
- View FFmpeg log output inside the app.

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

The compiled app is written to:

```text
build\VideoToAnimationTool.exe
```

## Run Compiled App

```powershell
.\build\VideoToAnimationTool.exe
```

## PowerShell Fallback

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\VideoToAnimationTool\VideoToAnimationTool.ps1
```

Both versions open as local desktop windows. They do not upload videos or frames.

## Basic Workflow

1. Click `Browse` in Source Video and choose an AI character action video.
2. Enter character and action names.
3. Choose an output folder.
4. Set start seconds, end seconds, export FPS, and image format.
5. Click `Export Frames`.
6. Use the preview panel to play the exported sequence.
7. Use `Frame Folder > Load` to preview an existing sequence folder without exporting again.

## Current Limits

- Windows-only.
- One source video per export.
- No sprite sheet, GIF, or video re-export yet.
- No AI cleanup or interpolation yet.
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
