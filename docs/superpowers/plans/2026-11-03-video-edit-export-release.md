# Video A–B Editing, Export and Portable Release Plan

> **For agentic workers:** superpowers:executing-plans, inline TDD, one economical independent final review. The user authorized continuation of the approved staged design; no repeat approval loop.

**Goal:** Complete increment5: frame-based repeated A–B Keep/Remove/Reset, current-edits preview, immutable file clipboard, atomic Save, explicit close/recovery/cache cleanup, and a portable0.5.0 build.
**Architecture:** Immutable source-frame interval model maps current timeline cuts back to original video. Render unique workspace outputs through selected FFmpeg encoding. An export session serializes edit/copy/save, snapshots revisions and stops playback before material changes. Preview owns user file-picker/clipboard/close interactions; recording workspace owns generated paths. Recovery lists validated app sessions without opening a main window automatically.
**Tech Stack:** Existing C#/.NET10/Avalonia12.1.4/NAudio2.3 and external FFmpeg. No new dependency/player/import subsystem.
**Spec:** docs/superpowers/specs/2026-11-03-inline-video-design.md (approved).

## Global constraints and review focus

- English code/comments/UI. Preserve screenshot/tray/hotkey behavior and all previous releases/data. No Git repository or invented merges/worktrees.
- No actual app, screen/compositor capture, system FFmpeg, audio devices, live clipboard or native UI tests by the agent. Synthetic files/streams/processes/headless tests only; real acceptance belongs to the user.
- Keep/Remove against edited timeline must preserve ordering and source bounds, reject empty output and keep A/V trimming aligned to the original frame grid.
- Cancel/error/dialog dismissal must preserve current preview and old destinations. No source/cache overwrite; final rename only after clean nonempty output.
- Copy publishes an immutable video file, retained across close/app exit. Only explicit cache cleanup removes those files; receiving application determines insertion behavior.
- Close/exit waits for export/playback resources. Unsaved preview offers Save/Discard/Continue; no deletion before devices/decoders/export jobs close.
- Recovery treats manifests as untrusted: only canonical GUID session children and recognized filenames, no links/path traversal/recursive deletion. Finalized captures can reopen; interrupted media is exposed for manual recovery, not claimed automatically repaired.

## Task1: Immutable edit timeline and render commands

Files: new Core/Editing/VideoEditTimeline.cs and VideoEditCommand.cs; Core.Tests.

- [x] RED tests for source-frame interval mapping, chained cuts, half-open A–B bounds, Reset, marker snapping, empty/invalid requests and deterministic fingerprint.
- [x] Implement Keep/Remove as slicing the current interval sequence into original frame spans; normalize adjacent spans. Build split/trim/setpts and audio asplit/atrim/pad/reset/concat graph, with structured encoder settings and no user command interpolation. Original/full clip with matching container may copy directly.
- [x] GREEN focused tests; no real FFmpeg.

## Task2: Render/export session, native file clipboard and safe storage

Files: App/Export renderer/session; Windows file clipboard encoder/publisher; RecordingWorkspace derived-file ownership; tests.

- [x] RED fixtures for rendering success/failure/cancel, repeated Copy reuse, immutable copies, old-destination preservation, container/extension matching, source/cache save prohibition, clipboard failure and cleanup ordering.
- [x] Implement shell-free job-owned render with bounded diagnostics/progress. Edit preview outputs stay in workspace; Save uses unique same-directory temp then atomic replacement; Copy renders into persistent data/clipboard and publishes Windows CF_HDROP/Preferred DropEffect with copy semantics. Test bytes only, never live clipboard. On failure keep original/current materialized media and make retry possible.
- [x] GREEN fixtures; stop/cancel/close must observe every job and never delete destination/cache files accidentally.

## Task3: Connect A–B controls, export and unsaved-close UX

Files: PreviewWindow/Host and playback Load cancellation, recording presenter/session/exit hook; headless tests.

- [x] RED tests for Set A/Set B and draggable range strip, Keep/Remove/Reset/Copy/Save icons, revision/preview updates, picker cancel, export cancellation, close Save/Discard/Continue and cleanup before ownership release.
- [x] Implement responsive wrapping strip with monitor-clamped size (including prior tiny-monitor placement note), current-edits render/load, cancelable busy state and progress. Pause/drain playback before edits/export; stop all jobs before session cleanup. Normal successful Copy/Save keeps preview open. Close/exit prompts for unexported edits and can cancel app exit.
- [x] GREEN headless integration and existing regression tests.

## Task4: Recovery and explicit cleanup through tray

Files: Recovery store/window, App tray events and composition. No auto-opening main/startup window.

- [x] RED tests for validated GUID directories/manifests/recognized names, missing/corrupt/linked/outside paths, finalized-session restoration and nonrecursive known-file cleanup. Copied-video cache survives session cleanup and is removed only by explicit cache action.
- [x] Implement tray Recover recordings window with icon Preview (finalized capture), Open folder (interrupted media), Discard and Clear copied videos. Reopen original plus validated edit intervals where possible; corrupt/missing edit data falls back visibly to retained source, never arbitrary paths. No automatic media repair claim.
- [x] GREEN tests; retain user files and expose actionable errors.

## Task5: Verification, source review and portable0.5.0

- [ ] Full scripts/build.ps1, one independent economical source review, material findings RED→GREEN in one pass, full suite after fixes.
- [ ] Version0.5.0; update README/settings reference/roadmap/manual and source/license package. Publish self-contained win-x64 without bundling FFmpeg, user data, logs or private tooling; preserve previous ZIPs.
- [ ] Verify package version, binaries/source/manual/license inclusion, hashes and absence of personal data. Deliver actual ZIP and explicit native acceptance checklist; don't mark real playback/capture/clipboard accepted.


