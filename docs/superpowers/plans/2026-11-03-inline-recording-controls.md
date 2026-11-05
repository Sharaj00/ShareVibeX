# Immediate Recording and Runtime Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Implement the second increment of the approved design: Record starts into a managed session without a destination dialog; source icons, capture-excluded runtime controls and real progress are connected; stopping retains a result for the subsequent preview increment.

**Architecture:** Preserve RecordingEngine as the owner of a single FFmpeg run, audio lifetime and safe finalization. Add a session workspace/controller above it, an independently capture-excluded compact control window and progress snapshots below it. SelectionOverlayController hands off the fixed physical region without a file picker. A completed result is retained as a typed pending-preview session; this increment does not pretend playback, Pause or live microphone switching have been implemented.

**Tech Stack:** Existing .NET 10/C#, Avalonia 12.1.4, external FFmpeg CLI, NAudio 2.3.0, Windows capture-exclusion/job APIs. No new player/library dependency.

**Spec:** `docs/superpowers/specs/2026-11-03-inline-video-design.md`, approved 2026-11-03.

## Global Constraints

- English source, comments and UI; Windows first with platform-neutral state/process contracts.
- Main application remains tray-only; settings/data remain beside the executable under data.
- No actual app, system FFmpeg, screen capture, audio devices, live clipboard or native UI automation in agent tests. Native acceptance belongs to the user.
- No uploads, accounts, automatic tool/model installation or silent codec/audio substitution.
- Preserve screenshot selection, fast Enter/Ctrl+C, in-place editor, geometry/icons and clipboard behavior.
- Existing files are untouched until an explicit successful Save. Temporary recording files are unique and session-owned.
- Implement inline with a single narrow independent final review, following the user's economy preference. This checkout has no Git repository: preserve earlier distribution artifacts; do not create commits/worktrees or invent a merge base.
- No public feature-release claim until the remaining playback/edit/export increments are connected. Ship the independent 0.4.1 parser correction now; retain the controls increment as reviewable source until the complete workflow is usable.

## Review Focus

- Stop/Cancel during startup must release a late audio source and retain no orphan recorder child.
- A right-edge region, negative coordinates or 150/200% scale must not move controls across the desktop or expose controls to capture.
- FFmpeg progress can contain N/A, negative values, partial lines or malformed numbers; UI must not show invented bitrate/duration or throw.
- Cancellation must delete only owned session files, retaining existing destination files and copied exports.
- Repeated Stop, natural duration completion or a failed recording must create at most one pending result/error and restore capture availability at the correct state transition.

## File Map

- Modify `src/ShareVibeX.Core/Recording/RecordingRuntimeContracts.cs`: immutable progress snapshot and polling contract.
- Modify `src/ShareVibeX.App/Recording/FfmpegRecordingProcess.cs`: parse bounded progress records and publish coherent snapshots.
- Modify `src/ShareVibeX.App/Recording/RecordingEngine.cs`: expose actual output dimensions/progress and maintain existing finalization boundary.
- Create `src/ShareVibeX.App/Recording/RecordingWorkspace.cs`: session directory, manifest, result identity and owned-file operations.
- Create `src/ShareVibeX.App/Recording/RecordingSessionController.cs`: region/audio/start/stop/cancel/result ownership above the engine.
- Modify `src/ShareVibeX.App/Recording/RecordingService.cs`: coordinate the session and relay state; preserve tray/hotkey callers.
- Create `src/ShareVibeX.App/Recording/RecordingControlWindow.cs`: compact vector controls/status with no full-screen input layer.
- Create `src/ShareVibeX.App/Recording/RecordingControlPlacement.cs`: pure physical/logical placement and safe outside-region fallback.
- Modify `src/ShareVibeX.App/Capture/CaptureModeToolbar.cs`: Record/Sound/Microphone/Back icons and independent source choices.
- Modify `src/ShareVibeX.App/Capture/SelectionOverlayController.cs`: remove production destination picker; hand off region and choices.
- Modify `src/ShareVibeX.App/Capture/DesktopCapturePresentation.cs`, `src/ShareVibeX.App/CompositionRoot.cs`, `src/ShareVibeX.App/App.cs`: wire session/window/tray lifetime and pending-result ownership.
- Add focused tests under `tests/ShareVibeX.App.Tests`; retain parser regression under `tests/ShareVibeX.Core.Tests`.

## Task 1: Coherent recording progress

**Interfaces:** Add immutable `RecordingProgress(TimeSpan Duration, long TotalBytes)` with nullable observed bitrate when duration/size are unavailable. `IRecordingProcess.Progress` exposes the latest snapshot; fake implementations return an empty snapshot. RecordingRun exposes the snapshot and actual `FfmpegCommand.Width/Height`; no measurement from the configured bitrate.

- [x] Write progress tests using a hidden plain console child, not FFmpeg: fragmented `out_time_us`, `total_size`, `progress=continue/end`, malformed/N/A/negative fields and final snapshot. Assert coherent media duration/bytes and calculated average bitrate; existing Ready/Stop behavior remains.
- [x] Run the focused test and confirm failure for absent progress behavior.
- [x] Parse only bounded progress records in `FfmpegRecordingProcess`; publish immutable snapshots at progress boundaries and retain latest valid values. Keep invariant parsing, bounded stderr and first-frame readiness.
- [x] Run `dotnet test tests/ShareVibeX.App.Tests -c Release --filter FullyQualifiedName~FfmpegRecordingProcessTests`; all tests must pass without any capture/device call.

## Task 2: Managed session storage and lifecycle

**Interfaces:** `RecordingWorkspace.Create(root, options, region)` returns a unique session with managed capture path and manifest. `RecordingSessionController.StartAsync(options, region, sound, microphone, token)`, `StopAsync()` and `CancelAsync()` serialize commands. State exposes Starting/Recording/Finalizing/Result/Failed plus a typed completed result for the later preview. Audio mapping is both off → silent, sound only → System, mic only → Microphone, both → SystemAndMicrophone; preserve selected device IDs. DirectShow remains explicit and incompatible combined requests fail before capture.

- [x] Write tests with fake process/audio and temporary directories: immediate start produces no destination-picker call; four source combinations map correctly; existing paths survive; session identity/path is unique; startup failure retains region/choices.
- [x] Add cancellation/late-source, repeated Stop, Stop during startup, natural completion and failed-output tests. Assert a single result event, existing safe partial preservation, no engine occupancy leak, and no cross-session deletion.
- [x] Run the tests and observe the new behavior fail.
- [x] Implement workspace manifest and session state above the existing engine. Validate all cleanup targets against the canonical session directory and recorded owned files; never accept arbitrary deletion paths from a manifest. Cancel stops before deleting, while unexpected failures retain recoverable output.
- [x] Wire media/progress dimensions from Task 1. A completed capture remains session-owned with an explicit pending-preview result; do not automatically overwrite a final destination or expose a disabled fake player.
- [x] Run the focused controller/workspace/engine tests; all must pass.

## Task 3: Ready icons and capture handoff

**Interfaces:** Replace the bool-only record callback with independent source choices, without destination ownership in SelectionOverlayController. Introduce a small immutable source-choice value shared by toolbar and session controller; retain test injection for start callbacks, not native file dialogs.

- [x] Add headless tests asserting exactly Record, Sound, Microphone and Back ready actions, vector icon content, short tooltips/accessibility names, independent toggle state and restored choices on failure.
- [x] Adapt live-overlay tests: clicking Record hands off the exact physical rectangle and options, hides overlays and waits for the existing compositor seam, never invokes a Save picker or takes a still frame. A failed start restores the same live region; successful handoff owns its own lifetime.
- [x] Run tests and observe the intended failure before implementation.
- [x] Replace text recording buttons with icons and toggles; map saved preferences to initial values, both off means silent. Remove the production recording Save picker and obsolete wide/video explanatory panel. Preserve camera/video/cancel choosing icons and screenshot keyboard paths.
- [x] Run `LiveOverlayIntegrationTests`, `LiveCaptureModeTests` and new toolbar tests; all must pass. No application window is launched by these headless fixtures.

## Task 4: Runtime controls and DPI/exclusion placement

**Interfaces:** `RecordingControlWindow` consumes a state/progress snapshot plus async Stop/Cancel callbacks. `RecordingControlPlacement` consumes physical region/monitor bounds, render scale and measured panel size and returns a monitor-local placement. `TryExclude` remains a platform seam and must succeed before the native surface is shown over capture content.

- [x] Add pure geometry cases for right-edge/narrow/full-monitor regions, negative origins, bottom edge, 100/150/200% scale and no outside space. Assert output is within the selected monitor and marked safe only when excluded or wholly outside capture bounds.
- [x] Add headless control tests for Stop/Cancel icons, duration/resolution/average bitrate/bytes formatting, starting/finalizing transitions, command serialization and stale updates after disposal. Pause/live mic actions are not displayed as working until the next increment connects them.
- [x] Write a fake exclusion/visibility test asserting exclusion is attempted before Show, refusal leaves no captured controls, and tray Stop remains reachable. Simulated topology changes stop safely without a stale window.
- [x] Run tests and confirm failure.
- [x] Implement the small own-chrome topmost panel; no full-desktop input overlay or dim layer. Use capture exclusion before display; fall back to fully outside-region placement, otherwise report refusal before starting. Poll bounded snapshots on the UI dispatcher; never query devices or run blocking file/process work on the UI thread.
- [x] Integrate composition and tray/hotkey Stop, including natural completion, failure, exit and panel disposal. Hand completed result ownership to the session for the later preview; retain the temporary file and report its path for source-level testing of this increment.
- [x] Run focused placement/control/lifecycle tests, then `./scripts/build.ps1`. Expect all tests passing, zero build warnings/errors, no real native capture/device checks performed.

## Task 5: Final source review and handoff to next increment

- [x] Review against the approved spec and the five Review Focus cases; dispatch one fresh narrow source reviewer. Fix material findings with a regression test seen failing before correction; no repeated broad review loop.
- [x] Record source-only status and test evidence in `.superpowers/sdd/2026-11-03-inline-video/progress.md`; do not mark native behavior accepted or claim playback/editing is present.
- [x] Preserve and verify the independently published 0.4.1 parser-fix ZIP, including version, licenses, source and manual. No existing portable data is moved or deleted.
- [ ] Next approved increment, outside this completed plan: continue with a plan grounded in the resulting interfaces: segmented Pause/Resume and dynamic microphone; then in-place A/V playback/seek; then A–B editing/copy/Save/recovery. These remain required before the new workflow is claimed complete and a feature release is handed off.

## Plan self-review

This plan covers only increment 2 of the approved design, with increment 1 independently completed as 0.4.1. It deliberately does not claim the full requested workflow complete: Pause/live mic/playback/A–B/clipboard/Save remain named subsequent increments. Each review-focus failure is owned by a specific task/test above. Source choice, physical geometry and process/session lifetime contracts are explicit. Native acceptance remains separate from internal evidence. The user approved implementation on2026-11-03. Tasks1–5 are complete as a source-only increment; evidence and remaining minor notes are in .superpowers/sdd/2026-11-03-inline-recording-controls/progress.md. Subsequent increments remain future work.
