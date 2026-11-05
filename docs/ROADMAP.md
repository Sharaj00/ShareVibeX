# ShareVibeX — next stages and settings/UI roadmap

Date: 2026-11-03. This plan follows the user's four ShareX reference screenshots and established ShareVibeX scope. Items below are planned, not features claimed in 0.3.2. Blurred, cropped or unreadable rows are intentionally not inferred.

Update 0.5.1: Stage 2 recording is implemented: immediate start, independent sound/mic, Pause/Resume, live mic, excluded controls/metrics, in-place A/V preview, seek, repeated A–B edits, durable file clipboard, atomic Save and retained-session recovery/cleanup. Native Windows/device/encoder acceptance remains pending the user's checklist in docs/testing/2026-11-03-manual-0.5.1.md. The 0.5.1 refinement unifies transparent toolbar placement, hover seek/A–B and frame steps, estimated live metrics and dashed recording borders. Ctrl-drag moving capture remains an assessed future backend change, not an enabled control. Stages 3–5 and 6 remain future work.

Update 0.6.0: opt-in capture history archives successful Copy/Save results under yyyy-MM folders. Text-processing features have been removed from the product scope.

## Current status — 0.10.1, 2026-11-04

Stage3 is implemented (English/Russian, tray and explicit Windows autostart). Stage4 completion previews, custom WAV and independent mouse actions are implemented; remaining event sounds, full-result Copy/Edit and monitor selection were deferred by the user. Stage5 advanced selection is implemented; native acceptance remains pending. Stage6 stabilization fixes pending-preview Exit and isolates unavailable history entries;386 internal tests pass, with no agent-run native acceptance.

Use TODO.md for active work and BUGS.md for verified defects versus acceptance checks. Prepared recording and moving the recording rectangle remain deferred architecture work, not enabled features. Earlier stage text below is historical design context; this status takes precedence.

Update0.11.1: History is available only through its Settings section; Refresh and recording recovery UI are removed. File actions always reconcile actual files. Rows are flattened, and AMOLED capture buttons use translucent black scoped resources. Legacy tray History actions migrate to Settings.410 internal tests pass; files and damaged-settings recovery are preserved.

## Baseline and invariants

Update0.11.0: History is a Settings section, including the existing tray shortcut. File activation, classic Windows Shell context menu and recycle-only red-cross action replace per-file Open/Folder buttons.405 internal tests pass. User reported0.10.2 baseline checks working; new native Shell behavior has a focused acceptance checklist.

Update 0.10.2: Stage6 internal release-readiness checks cover folder relocation, optional tool absence, capture-button accessibility in both languages and invariant legacy JSON migration.393 tests pass; native/long-run acceptance is still pending. Linux implementation boundaries are recorded in [linux-portability-assessment.md](linux-portability-assessment.md); no Linux runtime support is claimed.

- C# + Avalonia, Windows 11 first, future Linux adapters. English source/comments; English and Russian UI are implemented.
- No main window. Launch into the tray. No uploads, accounts or cloud history.
- Settings always live in `data` beside the executable. No alternative-folder setting or command-line override. Do not automatically move/delete external settings left by older versions.
- Select on the live undimmed desktop. During dragging show region resolution only. After release show screenshot/video/cancel icons with tooltips and accessible names. Enter/Ctrl+C before mode choice copy a fresh unedited crop. Freeze only on explicit screenshot-editor entry.
- Keep a small capture toolbar. Detailed options belong in compact Settings sections/tabs; expand rare advanced options and help on demand.
- Preserve existing settings when adding fields. Missing optional tools must not disable screenshots. Do not download tools/models or enable autostart merely by adding a selector.
- Each stage produces a runnable portable build, source, focused internal tests and a user checklist. Actual desktop/device/provider behavior is accepted by the user; internal tests are not native runtime proof.

## Delivery order

| Stage | Deliverable | Main acceptance |
| --- | --- | --- |
| 2 — implemented; native acceptance pending | Actual region video recording, silent and audio | Real output from the selected live region, correct audio, graceful Stop and usable finalized files |
| 3 | General, language, tray actions and Windows startup | Preferences persist; tray-only navigation remains reachable; autostart changes only on explicit user action |
| 4 | Sounds and in-app completion notifications | Events fire once, actions work, notifications stay out of capture and respect fullscreen suppression |
| 5 | Advanced region selection and capture presentation | Hover/snap/crosshair/monitor options work without changing default live/quick-copy behavior |
| 6 | Interface consistency, portable release validation and Linux preparation | Unified icons/help/navigation, verified Windows acceptance scenarios and documented platform boundaries |

Stages 3–5 are independent settings/UI batches after video acceptance. They must not be folded into one oversized recording implementation. Before each stage, create its detailed implementation plan against the then-current source and dependency versions.

## Stage 2 — recording implemented; native acceptance pending

The shared live region now enters four ready icons (Record/Sound/Microphone/Back), starts in a managed data/recordings session without a destination picker, and shows excluded runtime controls and measured metrics. Segmented Pause/Resume excludes wall-clock pauses; native mic switching releases disabled devices. Stop assembles and previews in the same region, with bounded FFmpeg decoding and NAudio audio output.

Frame-grid A–B edits compose against an immutable source. Copy retains an immutable Windows file-drop export under data/clipboard; Save renders beside the destination before atomic replacement. Unsaved Close/Exit offers Save/Discard/Continue. Tray Capture history opens completed Copy/Save archives. Its recovery action exposes validated finalized sessions and manual interrupted recovery, plus explicit known-file/cache cleanup.

Keep the user acceptance gate separate: actual capture exclusion, mixed DPI/negative coordinates, silent/system/mic/mixed audio, long-run sync, GPU encoding, repeated pauses/seeks/edits, clipboard receiver behavior, failure/recovery and repeated sessions require the 0.5.0 checklist. Internal/headless evidence does not close that gate. Original resolution is retained; preview is capped at 960×540/30fps. Custom command Pause and DirectShow/native mic mixing remain unsupported. Do not start another broad video implementation just because old stage descriptions mention planned recording.

## Stage 3 — General, language, tray and startup

### Language

Add a Language selector and a localization resource layer. Proposed first languages: English and Russian; English remains the existing default until the user selects otherwise. Cover settings, tray menus, capture tooltips, recovery, errors, notifications and provider status. Preserve layout with longer strings and Russian/English keyboard layouts. Developer comments remain English. Avoid partly translated pages.

### Tray behavior

Add independent left-click, double-left-click and middle-click actions. Applicable actions: Capture region, Open settings, Stop recording when available, and None. Proposed defaults: left = Capture region, double-left = Open settings, middle = None. Distinguish a double click from two single clicks so one gesture does not open capture then settings. Show unavailable actions clearly.

The screenshot's Open main window and Upload from clipboard actions do not apply: there is no main window or upload subsystem. Do not introduce either just to reproduce the menu.

Plan the Show tray icon option together with a dependable alternate way to reach Settings and Exit. Hiding the only control surface is not acceptable. Before enabling tray-less operation, provide verified global settings/exit commands and single-instance command routing (for example `--settings`), and restore the tray when that navigation is unavailable or conflicts. Default remains a visible tray icon.

Minimize to tray on start is already the normal tray-only behavior. Keep silent startup as the default; if a startup-visibility preference is useful, expose Open settings on startup as its meaningful alternative. This is an adaptation of the reference, not a new main window.

### Start with Windows

Add an explicit Run ShareVibeX when Windows starts toggle. Use a per-user startup entry without administrator privileges. Display actual registration state, handle a moved/missing portable executable and unregister only ShareVibeX's own entry. Merely opening Settings must not alter Windows. Keep Linux autostart behind a later platform adapter.

Acceptance: single/double/middle actions execute exactly once, shortcut conflicts never leave the application unreachable, silent startup remains tray-only, selected language persists, opt-in startup registration launches the correct executable and can be disabled cleanly. Tests/reviews do not install a real startup entry without user-directed verification.

## Stage 4 — sounds and completion notifications

### Sounds

Add the readable screenshot options: sound after capture, sound after completed action, and custom sound files for Capture, Task completed, Action completed and Error. Define each event before wiring it: acquisition is not successful save/mux completion, and one task must not emit duplicate completion sounds by default.

Use short local default sounds with recorded redistribution terms. Browse and preview custom files explicitly. A missing/invalid sound must not fail capture or file finalization. Keep capture/notification playback separate from recorded audio policy; do not accidentally record the app's completion sound into an active recording.

### Toast notifications

Implement in-app notifications so duration, fade, geometry and mouse actions can actually be controlled without requiring an installed Windows application identity. Include:

- Enable completion notifications; display duration and fade duration.
- Placement and width/height, clamped to the chosen monitor's work area and scaled DPI.
- Independent left/right/middle-click actions.
- Automatically hide during screen capture.
- Suppress notifications while a fullscreen application is active.

Applicable actions are Open saved file, Open output folder, Copy result, Edit image, Close and None, according to the result type. Open link is not mapped to an upload feature. Editing/copying a toast image requires explicit bounded result ownership/lifetime; it must not create a hidden screenshot archive. A quick copied screenshot is auto-saved only when capture history is enabled.

Proposed timing defaults can follow the reference (3 seconds display, 1 second fade); labels must define whether fade is included. Fullscreen suppression means exclusive Direct3D fullscreen only, per the later user clarification; borderless/maximized windows must allow feedback. Default capture behavior must exclude toasts, including notifications created while capture/recording is active; defer/suppress them rather than relying only on visual placement.

Acceptance: correct success/error event order, one notification per completion, actions for images/video/text, proper monitor/DPI placement, expiry while hidden, fullscreen suppression and no notification/sound contamination of recorded content. Real Windows behavior remains a manual scenario.

## Stage 5 — capture detection, information and overlays

Add the readable capture settings from the fourth screenshot:

1. Detect window regions under the cursor. Exclude ShareVibeX overlays and non-capturable/minimized surfaces; keep manual drag selection available.
2. Optionally detect child-control regions inside windows. Query bounds only; do not introduce text scraping or app-specific hooks. Avoid expensive detection loops when disabled.
3. Background dim strength. Preserve the latest live-selection requirement: live selection defaults to no dimming. Initially apply this control to the frozen editor; any later live dimming must be a separate explicit opt-in, not an automatic change to the default.
4. Custom information near the cursor. Document/validate tokens for position, dimensions and optionally RGB/hex/newline; do not guess the truncated template in the screenshot. Pixel-color sampling is optional and must not reintroduce an always-running full-screen capture loop.
5. Snap-size presets while Alt is held: add/remove named sizes, keep sizes in physical capture pixels, define anchor and modifier precedence, and validate against monitor bounds. Output video scaling stays independent.
6. Position and size information toggle. Default remains size-only while dragging; coordinates/additional information are opt-in.
7. Screen-wide crosshair and center crosshair as independent options. Default off to preserve the new minimal selection view. Crosshairs/labels must never enter exported pixels.
8. Restrict capture region and cursor to the active monitor. Default off, preserving cross-monitor selection. Define the active monitor at session entry; any native cursor confinement must be released on success, Escape, failure, display change and application exit.

Acceptance: hover choice does not accidentally select the overlay, nested-window bounds, all four drag directions, move/resize, Alt presets, modifier conflicts, negative coordinates, monitor gaps, mixed DPI, cursor release after every exit and fresh Enter/Ctrl+C capture. Live selection must remain live under these options.

## Stage 6 — consistency and release acceptance

Unify monochrome vector icons, accessible names, tooltips, keyboard focus and compact spacing across settings, capture, recording and notifications. Keep own Windows-11-style window chrome, System/Contrast themes, durable settings migration and application-local data. Introduce a settings search only if the expanded sections justify it; do not turn each parameter into a large card.

Validate performance, multiple monitors, mixed DPI, long-running recording, dependency failures and portable folder moves. Maintain licenses/source archives. Plan Linux capture/shortcut/audio/tray/autostart adapters with X11/Wayland/portal differences explicitly; compiling shared code does not establish Linux runtime support.



