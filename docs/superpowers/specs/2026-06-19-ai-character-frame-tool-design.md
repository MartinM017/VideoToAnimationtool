# AI Character Frame Tool Design

Date: 2026-06-19

## Goal

Build a Windows desktop tool that helps create AI character animation assets from generated character action videos. The first version focuses on making and previewing sequence frames: import a video, cut it into ordered image frames, inspect the result, and preview the frames as a looping character animation.

## Target User

The primary user is an AI animation creator who generates short character action videos and needs a reliable local workflow to turn those videos into usable animation frame folders.

## First-Version Scope

The first version includes:

- Windows-only desktop app.
- Import one source video at a time.
- Supported video inputs: `mp4`, `mov`, `webm`, and `avi`, subject to FFmpeg codec support.
- Select an output directory.
- Set character name and action name for output file naming.
- Set start time, end time, target FPS, and output format.
- Export sequence frames with predictable zero-padded names.
- Show export progress, current status, and a readable log.
- Load the exported frame folder into an animation preview panel.
- Preview sequence frames as a looping animation with adjustable playback FPS.
- Reopen an existing sequence-frame folder for preview.

Out of scope for the first version:

- Batch processing multiple videos.
- Sprite sheet export.
- GIF or video re-export.
- AI background removal, pose correction, frame interpolation, or image enhancement.
- Timeline editing beyond selecting start and end time.
- macOS or Linux support.

## Recommended Approach

Use a C# WPF desktop app for the Windows interface and call a bundled FFmpeg executable for video decoding and frame extraction.

Reasons:

- WPF fits a Windows-only local desktop app well.
- Native file pickers, folder pickers, progress display, and image preview are straightforward.
- The repo environment already has `.NET` available.
- Bundling FFmpeg avoids depending on a system-wide FFmpeg installation.
- The app can later be packaged as a Windows installer or self-contained folder.

Electron is not recommended for the first version because the current environment does not expose `node` or `npm`, and the packaged app would be larger. A script-based app is not recommended because the preview and frame-management workflow should feel like a real tool, not a temporary utility.

## User Workflow

1. User opens the desktop app.
2. User chooses an AI-generated character action video.
3. App displays basic video metadata when available, including duration and detected frame size.
4. User enters character name and action name.
5. User chooses output folder, target FPS, output image format, start time, and end time.
6. User starts export.
7. App runs FFmpeg and writes files like `character_action_0001.png`.
8. App shows progress and log output while export is running.
9. When export finishes, app loads the output folder into the preview panel.
10. User plays the sequence as a looping animation and adjusts preview FPS.
11. User can reopen an existing sequence folder later and preview it without reprocessing the source video.

## Main Screen Layout

The main screen has four functional areas:

- Source panel: video picker, file path, metadata, and optional open-file button.
- Export settings panel: character name, action name, output folder, time range, target FPS, image format, and naming preview.
- Progress and log panel: progress bar, status text, cancel button, and concise FFmpeg log output.
- Animation preview panel: frame canvas, play/pause button, FPS control, current frame counter, loop toggle, and folder reload button.

The app should open directly into the working interface. It should not have a marketing-style landing page.

## Frame Export Behavior

The export command should call FFmpeg with arguments equivalent to:

```text
ffmpeg -ss <start> -to <end> -i <input> -vf fps=<fps> <outputFolder>/<name>_%04d.<ext>
```

Implementation details may adjust argument order if testing shows more accurate seeking or better performance.

The output name is built from sanitized `characterName`, sanitized `actionName`, and a four-digit index:

```text
<character>_<action>_0001.png
```

If the target output folder already contains files matching the same naming pattern, the app should warn before overwriting. The first version can require the user to confirm overwrite or choose another folder.

## Animation Preview Behavior

The preview system loads image files from a folder in natural sequence order. It supports `.png`, `.jpg`, and `.jpeg`.

Preview controls:

- Play and pause.
- Preview FPS numeric control.
- Loop on/off.
- Current frame display.
- Manual previous and next frame buttons.

The preview should avoid loading very large folders into memory all at once if that causes memory pressure. The first implementation can start with a simple image cache and then improve it if testing reveals performance problems.

## Error Handling

The app should handle these cases clearly:

- No video selected.
- Unsupported or unreadable input file.
- Missing FFmpeg executable.
- Invalid time range.
- Invalid FPS.
- Output folder missing or not writable.
- Export cancelled by the user.
- FFmpeg exits with an error.
- Frame folder is empty or contains unsupported files.

Errors should be shown in plain language in the UI and include technical details in the log panel where helpful.

## Testing Plan

The first implementation should be verified with:

- Unit tests for filename sanitization and output name generation.
- Unit tests for time range validation and FPS validation.
- Manual export test with a short sample video.
- Manual preview test with an exported frame folder.
- Manual reopen test with an existing sequence folder.
- Manual cancel test during export.

## Future Extensions

Likely next features:

- Batch export for multiple action videos.
- Delete or mark bad frames.
- Export sprite sheets.
- Export GIF or preview video.
- Compare source video playback against exported frame playback.
- Presets for common animation FPS values.
- Project files that remember character/action settings.
