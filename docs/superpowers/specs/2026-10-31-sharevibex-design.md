# ShareVibeX design

Status: Approved by the user, including a dedicated Recording settings tab based on ShareX-style FFmpeg options and additional scaling controls. Implementation proceeds through separately reviewed stage plans.
Date: 2026-10-31

## User amendment — minimal controls and fixed data (2026-11-03)

During live region gestures show resolution information only near the cursor; hide the mode panel until the gesture ends. The chosen-region panel has three icons: screenshot editor, video, cancel, with tooltips and accessible names. Enter/Ctrl+C still copy an unedited fresh region. Remove the Portable data row from General; settings always use application-local data, superseding earlier optional-directory/--data-dir recovery proposals. Preserve invalid settings and offer retry/backup-reset/exit, not relocation. Future visible settings from the supplied references are scheduled in docs/ROADMAP.md; unreadable rows are not inferred. Actual video recording is the next implementation stage.

## User amendment — 2026-11-03 (supersedes freeze-on-invocation)

Select the working region on the live, undimmed desktop before choosing screenshot editing or video. Only explicit screenshot-editor entry freezes the current image. Enter or Ctrl+C before choosing a mode copies a fresh unedited screenshot. Video settings use compact ShareX-style video/audio columns rather than a long stack of large cards; retain all advanced/scaling options through secondary tabs and expandable help. Runtime recording remains its own forthcoming tranche, clearly identified when unavailable. Real desktop checks still belong to the user.

## User amendment — 2026-11-02 (takes precedence)



Editor/chrome amendment: System must use Windows 11 gray surfaces rather than Fluent's black SystemRegionBrush; replace native window decorations with a custom draggable header/caption buttons. Tool color opens a palette. Text is a blank on-canvas input rectangle resized to scale text, with no preset text/font fields. Remove Delete, undo/redo and Select/Move buttons. History shortcuts are Ctrl+Z / Ctrl+Shift+Z, Delete remains keyboard-only, Ctrl+drag moves annotations while preserving the chosen tool, and wheel changes the number and active-drag width/blur strength. Manual checks remain the user's responsibility.

## Intent and confirmed decisions

Build a focused, portable screen capture tool inspired by ShareX, without its upload and workflow ecosystem. The confirmed name is ShareVibeX. The user selected C# and Avalonia. Windows 11 is the primary platform; the architecture must allow a later Linux build. Code, comments, and UI strings are English unless requested otherwise.


## Approaches considered


## Scope and non-goals




## Proposed defaults requiring design approval

These are implementation proposals, not additional user decisions:

- Windows x64 is the first release target. Use a supported stable .NET SDK and stable Avalonia release, pinned during implementation planning.
- Distribute a self-contained Windows ZIP with no .NET installation requirement. Provide a smaller framework-dependent build only if useful later. No installer, service, or mandatory administrator privileges.
- Default shortcut: Ctrl+PrintScreen, configurable, with visible handling of conflicts. A tray command and main-window Capture button invoke exactly the same session entry point.
- Launch to a compact main window; closing it minimizes to tray, with an explicit Exit action. Optional start minimized; no automatic startup registration in the initial release.
- Default screenshot output is PNG. Copy to clipboard and Save As are explicit actions; no automatic screenshots archive. Output directory is configurable and remembered.
- Default silent video is H.264 MP4 without an audio track, labeled Silent / GIF-style. Actual GIF export is an optional recording preset, not the default container.
- Record with audio defaults to system playback audio. Settings allow system audio, microphone, or both and device selection. Missing audio capture must not silently produce a silent recording.
- Proposed application license: GPL-3.0-or-later, allowing compatible reuse from ShareX if needed. Approval of this design approves that license choice; it does not authorize publishing a repository or release. Third-party components and model weights retain their own terms.

## Capture session and interaction

The common entry point creates a CaptureSession containing a desktop snapshot, selected region in physical screen pixels, monitor geometry, and current mode. This is one shared user workflow, not a requirement to use one low-level API for still images and video frames.

State flow: Idle -> Selecting -> Editing; Editing -> TextPanel or VideoReady; VideoReady -> Recording -> Finalizing -> Result. Escape cancels selection; from editing it discards the unsaved session with a confirmation only when edits exist. Escape does not silently discard a recording.

At invocation, hide application UI and obtain the desktop snapshot before showing the selection overlay. After a rectangle is selected, show a floating toolbar adjacent to it, clamped to visible screen bounds. The screenshot remains frozen while editing. Region resizing or selecting again before recording is available through a dedicated action; a new selection resets annotations after confirmation if needed.


On video start, remove the frozen overlay and capture live frames from the same physical rectangle. The toolbar must not appear in the recorded output. Place recording controls outside the region, provide a stop shortcut and tray Stop action, and indicate elapsed time. Do not promise an on-screen border that can never be captured until the chosen backend is verified.

Allow a region across monitors. Normalize negative desktop coordinates and mixed display scaling into physical pixels. Revalidate bounds when displays change. HDR to SDR appearance and mixed-scale boundaries require real-device checks and must be documented if limited.

## Screenshot editor

Tools: rectangle, ellipse, freehand pen, line, arrow, and text. Rectangle/ellipse tools support outline, solid fill, or blur of the underlying image inside the shape. Allow color, stroke width, and blur strength controls. Include move/select, delete, undo, and redo. Text supports font size and color; use sensible installed-font fallbacks.

Store annotations as editable objects over an immutable source bitmap. Render the same composition for preview, clipboard, and saved output. Blur must be applied to the final pixels within its mask, not simulated by a translucent shape. Blurring is a visual effect, not a guarantee of secure redaction; solid opaque fill is also available.

Copy or save flattens the composition. Editing originals are discarded when the session ends; editable project file persistence is outside the initial scope. Verify arrowheads, text placement, ellipse masks, and export scaling.

## Video and FFmpeg

FFmpeg performs encoding/muxing; frame acquisition is an independent backend. Windows capture uses suitable Windows APIs or a verified FFmpeg input; audio uses a verified system loopback/microphone path. The recording milestone starts with a narrow backend feasibility check, including capture exclusion and timestamps, before committing to a specific input pipeline.

Expose two primary start buttons: Record with audio and Record silently. Advanced settings live in Settings, not the region toolbar: container, codec, frame rate, quality/bitrate, preset, hardware encoder, cursor inclusion, output path, and audio source/device. Show only supported combinations or explain incompatibilities before starting.

Probe the configured FFmpeg executable for version, inputs, and encoders. Hardware encoding is selectable when available; use a clear software fallback. An advanced encoder-arguments field accepts a parsed argument list passed without a shell; validate conflicts with application-owned capture/output arguments. Show the effective command with secrets redacted where applicable.

### Dedicated Recording settings tab (confirmed addition)

Provide Settings > Recording as a separate tab, informed by the user's ShareX screenshot and the upstream FFmpegOptions / ScreenRecordingOptions implementations. Adapt option semantics and command generation; do not inherit unrelated ShareX subsystems or require recorder-device installation.

Groups: FFmpeg executable and capability test; video/audio input sources and devices; container and frame rate; video encoder and codec-specific quality/bitrate, preset, tune, and rate-control choices; audio encoder, bitrate, sample rate, and channels; cursor; scaling; additional arguments; custom command template; effective command preview; reset to defaults. Display controls only where meaningful for the selected backend/codec and validate against the actual configured FFmpeg build. Selecting an encoder from a list does not prove the local hardware can initialize it.

Scaling has explicit modes: Original, Percentage, and Target size. Percentage is output/input multiplied by 100 (50 means half width and height). Target size accepts width and/or height, preserves aspect ratio by default, and supports a selectable scaling algorithm. Show resulting dimensions; validate positive finite values and encoder limits, round/pad to encoder-required alignment, and keep capture coordinates independent of output dimensions. Include a convenient divide-by-factor control/preset to reproduce reductions such as 1/1.5 in the screenshot without manually editing filters. Default to original size. Integrate filters in a single ordered pipeline with pixel-format conversion and any hardware upload/download. Reject incompatible duplicate scale/filter options rather than silently overriding them.

Normal mode generates arguments from controls. Advanced custom-command mode replaces the generated input/encoding argument body with an editable FFmpeg argument template, with syntax help, preview, validation, and Reset. Support ShareX-style placeholders $fps$, $area_x$, $area_y$, $area_width$, $area_height$, $cursor$, $duration$, and $output$. Define $duration$ as the selected positive time limit; if no time limit is configured, reject its use and explain why. Expand placeholders into argument values without invoking a shell. Reject unknown placeholders. In custom mode, show which structured controls are overridden; scaling must either be represented by a supported template fragment ($scale_filter$) or explicitly reported as controlled by the custom template. Silent recording always rejects an effective command that would write an audio track. Keep application-managed output finalization: require exactly one output destination matching the temporary output path expanded through $output$ and reject extra outputs. Custom FFmpeg arguments can change capture sources and filters, but cannot execute shell commands. Do not copy a hardware-specific command from the example as the universal default.

Default to an even-sized encoding frame by padding when necessary, preserving the selected visible content. Keep video/audio clocks synchronized. Stop gracefully and wait for muxing before declaring success. Write to a temporary output in the destination directory and rename on success. On disk exhaustion, process crash, device loss, or cancellation, report the error and retain recoverable output where possible. Closing during recording offers Stop and save or Continue recording.







## Portability and dependency discovery

Resolve optional tools using: explicit user path override; detected system installation/PATH; optional application-local tools directory. The default with no override prefers installed system tools. Validate executables, do not merely check file existence. Use argument-list process APIs, never shell command concatenation.

Store settings and operational data under an application-local data directory. On a read-only application directory, explain the issue and allow selecting a writable data folder; do not silently migrate to a user-profile directory. Avoid storing images in logs and clean temporary images at the end of the session, including cancellation/error paths where possible.

API credentials are not included in exported settings. Proposed credential handling: Windows-protected storage for normal use and an optional session-only key, with a clear indication that protected credentials will not move between computers. Application configuration and captures remain portable; credentials can be re-entered on another machine. Linux secret storage is a later platform-specific implementation.

Provide a dependency/settings page with detected paths, validated versions, manual Browse, Test, and setup guidance. Do not install system packages, register file associations, or change PATH automatically.

## UI and themes

Use Avalonia's Fluent styling as a base, with explicit application tokens for light, dark, and AMOLED. AMOLED uses true black surfaces with visible control boundaries and focus states. Follow Windows 11 spacing, typography, rounded controls, and restrained effects; do not require Windows-only visual effects for usability.

Keep main navigation limited to Capture, Settings, and About/help. Include keyboard tool shortcuts, visible focus, tooltips, accessible names, and readable contrast. The editor toolbar is compact; provider setup and FFmpeg detail belong in Settings.

## Architecture and Linux boundary

Separate a platform-neutral domain/core (session state, regions, annotations, settings, provider contracts), Avalonia presentation, Windows system integration, external process/provider adapters, and tests. Project/file organization is left to implementation planning.

Abstract desktop snapshots, live frame capture, monitor geometry, global shortcuts, audio acquisition, clipboard, tray behavior, and credential storage. Keep Windows types out of core data contracts. FFmpeg settings and annotation rendering are shared when feasible.

A future Linux integration must account for X11 versus Wayland. Wayland may require ScreenCast/Screenshot and GlobalShortcuts portals and PipeWire, with system-mediated permissions. Compile-ready abstractions do not constitute a working Linux capture backend; that backend and distribution testing are separately scoped.

## Delivery stages and acceptance

1. Foundation and still capture: pin tools; create application, portable settings, three themes, global shortcut/tray entry points, region selection, PNG save and clipboard copy. Verify on Windows before expanding the editor.
2. Editor: deliver the complete requested annotation tools and shared preview/export rendering, undo/redo, and object selection. Verify mask boundaries and exported pixels.
3. Recording: validate input pipeline, then silent MP4, system sound/microphone recording, stop/finalization behavior, and advanced FFmpeg controls. Verify actual captured files including audio, odd-sized regions, and missing dependencies.
6. Release: portable ZIP, dependency/setup documentation, licensing notices, startup/capture resource measurements, and multi-monitor/mixed-DPI checks. Linux build/runtime work is a subsequent milestone.

Each stage receives a detailed implementation plan, focused verification, and a short user-facing handoff with what works and what remains. Do not claim real desktop, device, or external service behavior from unit tests alone. Add tests for consequential state transitions, coordinate transforms, rendering/export behavior, argument construction, and stale asynchronous results; avoid tests that only mirror trivial implementation details.

## Constraints, sources, and open implementation decisions


Primary references:
- https://github.com/ShareX/ShareX
- https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/ScreenRecording/FFmpegOptions.cs
- https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/ScreenRecording/ScreenRecordingOptions.cs
- https://docs.avaloniaui.net/docs/supported-platforms
- https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html

The project directory was empty during initial inspection. The current environment has .NET runtimes but no SDK discoverable by dotnet --info; implementation needs a local SDK or an existing SDK located outside PATH. Dependency installation belongs to the implementation stage after design approval.

Builder-owned decisions: supported stable package versions, exact capture/audio API and libraries after the recording feasibility check, MVVM library choice, rendering implementation, default FFmpeg quality/frame rate, detailed test framework, packaging scripts, and a replaceable initial accent color. Do not treat the design as evidence that these have been tested.

## Design review

A separate builder review identified application licensing as the only material unanswered decision. The GPL-3.0-or-later proposal above resolves it for user review. No other blocking design questions were reported.
