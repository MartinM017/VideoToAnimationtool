# AI Character Frame Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows local desktop tool that exports sequence frames from AI character action videos and previews frame folders as looping animation.

**Architecture:** Implement the first version as a PowerShell WinForms desktop application because the environment has Windows PowerShell and .NET desktop runtimes but no .NET SDK for compiling WPF. Keep reusable behavior in PowerShell modules under `src/VideoToAnimationTool/Core`, and keep UI wiring in `src/VideoToAnimationTool/VideoToAnimationTool.ps1`.

**Tech Stack:** Windows PowerShell 5, .NET WinForms, FFmpeg CLI, custom PowerShell test scripts.

---

## File Structure

- `src/VideoToAnimationTool/Core/PathUtils.psm1`: filename sanitization, natural frame sorting, image-frame discovery.
- `src/VideoToAnimationTool/Core/Validation.psm1`: FPS, time range, and output option validation.
- `src/VideoToAnimationTool/Core/Ffmpeg.psm1`: FFmpeg discovery and command argument construction.
- `src/VideoToAnimationTool/VideoToAnimationTool.ps1`: WinForms desktop UI and event handlers.
- `tests/Core.Tests.ps1`: dependency-light test runner for core behavior.
- `README.md`: usage, FFmpeg setup, and feature summary.
- `.gitignore`: ignore local brainstorm/session files and generated output folders.

## Tasks

### Task 1: Core Tests

**Files:**
- Create: `tests/Core.Tests.ps1`

- [ ] **Step 1: Write failing tests for filename, validation, sorting, and FFmpeg argument behavior**

Create tests that import modules which do not exist yet and assert:

- `ConvertTo-SafeName 'Hero Girl!*'` returns `Hero_Girl`.
- `New-FrameFileName -CharacterName 'Hero' -ActionName 'Run' -Index 7 -Extension 'png'` returns `Hero_Run_0007.png`.
- `Test-FrameExportOptions` accepts a valid range and rejects zero FPS.
- `Get-FrameFiles` returns `walk_1.png`, `walk_2.png`, `walk_10.png` in natural order.
- `New-FfmpegFrameExportArguments` contains `-ss`, `-to`, `-i`, `-vf`, `fps=12`, and an output pattern ending in `Hero_Run_%04d.png`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Core.Tests.ps1`

Expected: FAIL because the modules are missing.

### Task 2: Core Modules

**Files:**
- Create: `src/VideoToAnimationTool/Core/PathUtils.psm1`
- Create: `src/VideoToAnimationTool/Core/Validation.psm1`
- Create: `src/VideoToAnimationTool/Core/Ffmpeg.psm1`

- [ ] **Step 1: Implement filename and frame utilities**

Implement safe names, zero-padded frame names, natural sorting, and image-frame discovery.

- [ ] **Step 2: Implement validation utilities**

Implement validation for source path, output path, FPS, output format, and start/end time.

- [ ] **Step 3: Implement FFmpeg helpers**

Implement FFmpeg discovery and argument construction without running FFmpeg in tests.

- [ ] **Step 4: Run tests to verify they pass**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Core.Tests.ps1`

Expected: PASS.

### Task 3: Desktop UI

**Files:**
- Create: `src/VideoToAnimationTool/VideoToAnimationTool.ps1`

- [ ] **Step 1: Build the main WinForms window**

Create panels for source video, export settings, progress/log, and animation preview.

- [ ] **Step 2: Wire file and folder pickers**

Support video selection, output folder selection, and existing frame folder selection.

- [ ] **Step 3: Wire export**

Validate options, build FFmpeg arguments, run FFmpeg in a background job/process, stream log output, support cancel, and load frames after successful export.

- [ ] **Step 4: Wire preview**

Load frame folders, display current frame, play/pause with a timer, previous/next controls, preview FPS control, and loop toggle.

### Task 4: Documentation and Local Hygiene

**Files:**
- Create: `README.md`
- Create: `.gitignore`

- [ ] **Step 1: Document usage**

Explain how to run the app with PowerShell, where to place `ffmpeg.exe`, and what the first version supports.

- [ ] **Step 2: Ignore generated/local files**

Ignore `.superpowers/`, `output/`, `exports/`, and common temporary files.

### Task 5: Verification

**Files:**
- Read and verify all created files.

- [ ] **Step 1: Run automated core tests**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Core.Tests.ps1`

Expected: PASS.

- [ ] **Step 2: Parse the desktop script**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -Command "$null = [scriptblock]::Create((Get-Content -Raw 'src/VideoToAnimationTool/VideoToAnimationTool.ps1')); 'Parse OK'"`

Expected: `Parse OK`.

- [ ] **Step 3: Inspect git status**

Run: `git status --short`

Expected: created files are visible; staging may be blocked by sandbox `.git` permissions.
