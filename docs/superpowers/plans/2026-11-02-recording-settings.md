# Recording settings and FFmpeg foundation implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline; one independent review at the end.

**Goal:** Deliver the first independently reviewable recording tranche: functional Video settings, FFmpeg discovery/capability testing and deterministic, validated command generation. Recording start/stop and WASAPI capture follow in the next tranche.

**Architecture:** Shared recording options, scaling, token parsing, command building and capability parsing live in Core. A bounded asynchronous process runner and dependency locator live in App/Recording. Settings > Video edits a draft and previews commands; it does not start desktop or audio capture.

**Tech stack:** Existing .NET 10 / Avalonia 12.1.4, external user-installed FFmpeg; no new dependency or bundled FFmpeg binary.

**Spec:** docs/superpowers/specs/2026-10-31-sharevibex-design.md, recording, portability, dedicated settings sections.

## Global constraints

- English source, comments and UI; Windows 11 primary, Linux boundary preserved.
- Reuse current checkout; no Git repository exists. No worktree or commits can be created here.
- User approved the whole architecture and advancing to recording. Original request explicitly requires delivery in parts; this plan is the first recording part, not a claim recording is complete.
- User performs desktop verification. Never launch capture, native harness or live clipboard; synthetic/internal tests only.
- No dependency installation, FFmpeg redistribution, automatic capture or audio-device probing.

## Approaches and ruling

The approved spec delegates the exact recording backend to implementation. GDI `gdigrab` is the first video input because it supports physical desktop rectangles and negative offsets without drivers; WASAPI system/microphone audio will feed a local pipe in the recording-runtime tranche. Alternative desktop duplication requires adapter/output-specific handling, and installed DirectShow loopback drivers conflict with the portable default. Preserve an explicit DirectShow audio choice for users who already have such a device. A device listing action may be added with runtime capture; this tranche never silently substitutes an audio source.

## Review focus

- Old settings preserve theme, shortcut, folder and image options and gain recording defaults.
- Odd/negative-coordinate regions and percentage/divide/target scaling produce independent capture/output dimensions without losing source pixels.
- Extra/custom arguments cannot introduce extra outputs, override managed output, bypass silent mode or duplicate normal-mode filters.
- Missing/broken/hung FFmpeg never prevents screenshots or freezes settings; process output/time are bounded.
- Closing settings while a test is running cancels it; stale test results never claim a changed path was validated.

## Tasks

### 1. Recording options and argument construction

Create Core/Recording/{RecordingOptions,VideoScaling,FfmpegArguments,FfmpegCommandBuilder}. Extend AppSettings with optional RecordingOptions and normalize old settings on load. Consume PixelRect and produce `FfmpegCommand(Arguments, Width, Height)` from options, region, audio mode and managed temporary output path. Audio pipe arguments are supplied as values by the future runtime adapter. Support MP4/Matroska, H.264/H.265 software and common hardware encoder presets, quality/bitrate, audio format, cursor, duration, scaling, safe additional option pairs and restricted custom templates.

- [x] Write failing regression tests for scaling/padding/stride-independent coordinates, paths with spaces, custom placeholders/output ownership/silent validation and migration.
- [x] Observe RED, implement and verify GREEN.

### 2. FFmpeg discovery and probing

Create App/Recording/{FfmpegLocator,ProcessCommandRunner}; Core/Recording/{FfmpegProbe,ICommandRunner}. Resolution: explicit override (invalid is an error), PATH, known system installation candidates, then app-local tools. Probe version, devices, encoders and filters without starting capture; expose clear capability information. All child processes use ArgumentList, no shell, bounded output, timeout and cancellation.

- [x] Write failing parser/fake-runner tests and dependency-resolution tests; verify RED then GREEN.
- [x] Ensure probes query capabilities only and do not enumerate or open capture/audio devices.

### 3. Settings > Video

Create Views/VideoSettingsPage; reuse existing card/heading/row visuals. Modify SettingsWindow to save recording draft. Include tool path/Browse/Test, probe status, sources, audio choices, video/container/rate settings, cursor/duration, scaling and dimensions, additional arguments, custom mode and effective command preview for audio/silent. Preview clearly labels an example region and future pipe path. Reset recording settings only. Closing cancels probe.

- [x] Add headless draft/save/reset and stale-probe tests that exercise real controls.
- [x] Implement without changing screenshot defaults/toolbar.

### 4. Review and delivery

- [x] Full internal build/tests, independent source review and focused fixes.
- [x] Portable 0.3.0 with explicit recording-foundation scope, README and VERIFY checklist prepared. Do not describe this tranche as a working screen recorder.
- [x] Verify archive/source/version without launching app. Report next tranche explicitly.
