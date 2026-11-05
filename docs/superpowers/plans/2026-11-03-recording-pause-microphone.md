# Recording Pause and Live Microphone Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Execute inline with TDD and one final independent economical source review.

**Goal:** Implement increment3 of the approved inline-video design: segmented pause/resume, cumulative media time, live microphone control and final segment assembly.
**Architecture:** RecordingEngine continues to own each FFmpeg segment. A serialized session controller owns ordered segments and natural exits. A paced live PCM source opens/releases microphone capture on demand; silence preserves stream topology when sources are off. Final assembly normalizes segment video/audio timestamps; fully silent sessions omit audio in their final file.
**Tech Stack:** Existing .NET10/C#/Avalonia12.1.4/NAudio2.3 and configured external FFmpeg; no new dependency.
**Spec:** docs/superpowers/specs/2026-11-03-inline-video-design.md (approved). User explicitly requested continuation of the already approved staged work; preserve inline execution and do not repeat approval questions.

## Global constraints

- English code/comments/UI. Windows first; preserve platform-neutral contracts.
- No native app/capture/compositor/FFmpeg/audio-device/clipboard runs by agents; headless, synthetic and plain console tests only.
- No new portable feature release until playback/edit/export increments are connected. Preserve existing0.4.1 ZIP.
- No Git repository: keep changes and a named ledger in place, no invented commits/worktrees.
- No silent device/encoder substitution. Preserve previous segments on failure; cancel removes only known owned files after processes/devices close.

## Review focus

- Pause/Stop/Cancel/natural-exit races consume each segment once and never deadlock.
- Resume startup failure retains earlier media and allows retry/Stop.
- Mic enable failure leaves previous source state and recording intact; off releases actual hardware, with no stale queued samples after disabling.
- Duration limits apply to cumulative media rather than each resume; paused wall time never contributes.
- Assembly cancellation/error retains segments, cannot overwrite saved files, and closes its child process.

## Task1: Live audio contracts and Windows implementation

Files: Core/Recording/IRecordingAudioSource.cs and RecordingRuntimeContracts.cs; Platform.Windows/Recording/WindowsRecordingAudioFactory.cs plus a small injectable input/lifecycle unit; App/Recording/RecordingEngine.cs; synthetic audio/engine tests.

Interfaces: ILiveRecordingAudioSource exposes MicrophoneEnabled and SetMicrophoneEnabledAsync(bool,CancellationToken); ILiveRecordingAudioFactory.OpenLiveAsync(RecordingOptions,RecordingSourceChoices,CancellationToken). RecordingRequest.LiveSources optional; when present internal WithAudio=true and engine opens the live factory. RecordingRun exposes live control. Native sources with both inputs off still emit paced silence without opening devices. DirectShow retains fixed source semantics and explicitly cannot toggle a native microphone during an active DirectShow run.

- [x] Write failing synthetic tests for silence without device open, on/off/repeated toggles, failed enable rollback, release/cancellation/disposal and existing fixed-source behavior; run RED.
- [x] Implement serialized device lifecycle, buffer snapshots and per-input stop suppression; retain paced pipe and bounded buffers. Cancellation must clean any opened candidate without publishing it. Engine cleanup/start deadlines remain.
- [x] Run focused audio/engine tests GREEN.

## Task2: Workspace segments and safe final assembly

Files: RecordingWorkspace.cs; new Core RecordingSegment/assembly-command model; new App RecordingAssembler.cs; process progress adds optional final Frames.

Interfaces: workspace creates unique indexed segment capture/working pairs and owns them in memory; manifests list relative owned segments but never authorize deletion. IRecordingAssembler.AssembleAsync(options,workspace,segments,keepAudio,token) returns a RecordingResult. A one-segment compatible output is copied through a fresh working file; stripping an internal silent track uses remux. Multiple compatible segments are transcoded with video PTS reset and audio trim/pad/reset to the segment's frame-derived duration, then concat. No custom command interpolation. Custom recording commands keep single-run behavior; Pause rejects them explicitly while recording continues.

- [x] Add RED tests for multiple owned segments/cleanup, final frame count parsing, ordered assembly graph, normalized duration/audio, silent output and cancellation/failed child preservation.
- [x] Implement known owned paths, safe atomic finalization and process/job cleanup. Durations use frames/frame-rate where available, otherwise reported time. Reject assembly when required media metadata is missing rather than inventing it.
- [x] Run focused tests GREEN. Real mux validity/A–V sync remain user acceptance.

## Task3: Serialized session pause/resume and mic control

Files: RecordingSessionController.cs, RecordingService.cs and session tests.

Interfaces: PauseAsync(), ResumeAsync(CancellationToken), SetMicrophoneEnabledAsync(bool,CancellationToken); Pausing/Paused/Resuming states are session-active. Controller has one transition semaphore; Stop/Cancel signal startup/assembly cancellation before waiting for it. Session Completion completes once for the whole recording, not on each paused segment. Current-run observers verify identity under the same semaphore before consuming a natural exit.

- [x] RED tests: repeated pause/resume, Stop while paused, natural completion, resume failure/retry, cumulative duration limit, failed mic enable, paused mic changes without opening devices, cancel during transition/assembly and preserved previous regression cases.
- [x] Implement session-owned ordered segments and counters, cumulative progress frozen while paused, stable live-audio track and ever-audible flag. Reopen selected endpoints on Resume; do not include pause wall time. Preserve original capture and owned failure paths.
- [x] Run session/engine/assembler tests GREEN.

## Task4: Connected runtime icon controls

Files: RecordingControlWindow.cs, RecordingControlPresenter.cs, RecordingService.cs; UI tests.

- [x] RED tests: Stop/Pause/Microphone/Cancel vector icons, Pause→Resume icon/name, selected microphone state, rollback after failed enable, disabled transition commands with Cancel reachable and Paused still owned by tray/hotkey/topology stop.
- [x] Connect actual actions and states. Keep tooltips inside excluded surface. Show action errors separately from retained paths; keep all native acceptance caveats.
- [x] Run focused headless tests and full scripts/build.ps1 GREEN.

## Task5: Review and source handoff

- [x] One fresh source review of lifetime/clock/cleanup races; reproduce material findings RED and fix in one pass, then full GREEN suite.
- [x] Update source-only docs and named ledger with evidence, decisions/limitations; do not claim native acceptance or overwrite0.4.1 ZIP.
- [x] Next increment remains real in-place A/V preview/seek, followed by A–B/copy/Save/recovery.

Self-review: shared live-source contracts feed engine/session/UI; per-segment progress feeds timing and assembler, all under explicit ownership. Reviewed states and failure classes have test owners. This plan implements only increment3, with later preview/export intentionally pending.
