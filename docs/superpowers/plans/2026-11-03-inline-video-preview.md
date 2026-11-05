# In-place Video Preview Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans, TDD and one economical independent final source review. User authorized continuation of the approved staged design; no repeated design/plan approval loop.

**Goal:** Increment4: after Stop, display the recording inside the selected physical region, initially paused; support audio/video playback, pause and generation-safe seeking.
**Architecture:** Session exposes immutable output metadata already known from recording. Separate FFmpeg video/audio decoders stream bounded BGRA/f32 PCM; Windows NAudio output supplies the playback clock. A playback controller serializes commands and cancels obsolete generations. Avalonia preview windows own only the selected rectangle and a compact control strip, while session ownership remains pending until asynchronous close completes.
**Tech stack:** Existing .NET10/C#/Avalonia12.1.4/NAudio2.3/external FFmpeg. No player dependency or implicit ffprobe requirement.
**Spec:** docs/superpowers/specs/2026-11-03-inline-video-design.md, already approved.

## Constraints and review focus

- English code/comments/UI. No actual app, FFmpeg, desktop/compositor capture, audio-device enumeration/playback or live clipboard in agent checks. Use synthetic/fake streams, headless UI and plain console tests only.
- Preserve screenshot behavior, segmented recording, own data folder and published0.4.1 ZIP. Source-only increment; A–B/copy/Save/recovery remain next.
- No Git repository: keep named ledger/source in place, no invented commits/worktree.
- Bounded buffers and frame ownership; drop late frames, prevent old generation callbacks, cancel/drain before replacing decoder or releasing capture ownership.
- Audio failure must be visible; never silently play a supposedly audible result without sound. Silent media never opens audio output.
- Close/exit/seek during decoder/device setup must release late resources and must not block the UI thread. Failures retain source files.
- Physical region/monitor scaling/aspect ratio/mixed-DPI clipping must remain explicit; only the selected region intercepts input. No full-desktop dim layer or independent player window.

## Task1: Preview contracts, FFmpeg streams and frame parsing

Files: new Core/Playback contracts; App/Playback decoder/command/frame reader; session metadata getter. Decoder output is explicitly bounded to preview dimensions, capped960×540 and30fps. Keep original source frame rate separately for future edit markers. Own recorded duration/audio state, no arbitrary import support.

- [x] RED tests for command arguments (seek/video scale/audio selection), fragmented/truncated BGRA data, bounded dimensions, disposal and known recording metadata.
- [x] Implement shell-free hidden processes with existing Windows job lifetime, bounded stderr, asynchronous raw streams, cancellation-safe cleanup and no Ready requirement for a still thumbnail.
- [x] GREEN focused fixture tests; no real FFmpeg run.

## Task2: Bounded Windows preview audio output

Files: new Platform.Windows/Playback ring/provider/output adapter; synthetic provider tests. Float PCM stereo48kHz, bounded queue with backpressure, prebuffer before starting, clear on disposal, expose WASAPI clock and explicit errors. No device open until Play on media with audio. Dispose on worker to avoid native thread join on UI.

- [x] RED tests for ordering, backpressure, completion/drain, partial sample alignment, cancellation and disposal using synthetic PCM; no physical playback.
- [x] Implement provider/sink and async factory; device failure/underrun is explicit. Position uses source-compatible shared-mode WasapiOut output format. No silent fallback.
- [x] GREEN focused provider tests.

## Task3: Playback lifecycle and superseding seeks

Files: App/Playback/RecordingPlaybackController.cs and fixture tests. APIs LoadAsync(media,format), PlayAsync(), PauseAsync(), SeekAsync(position), DisposeAsync(). Commands increment a generation and cancel the previous run before queuing; queued obsolete commands return without opening another decoder. Paused thumbnail decoding opens no audio device. Render callbacks are awaited, not accumulated in the dispatcher.

- [x] RED tests for initial paused frame, audio/silent clocks, pause position, end/replay, rapid seeks with stale open/read results, close during setup, decoder/audio error and resource cleanup.
- [x] Implement monotonic silent clock, audio-leading clock, bounded frame scheduling and EOF/drain behavior; retain source on errors. Late opens are disposed, all producers observed.
- [x] GREEN controller/decoder/audio fixtures.

## Task4: In-place preview UI and recording handoff

Files: App/Playback preview host/window/geometry, RecordingControlPresenter, Session, CompositionRoot and App exit hook. Separate video surface at original region and adjacent compact strip with Play/Pause, seek timeline, time and Close icons. Black letterboxing; first frame paused. No A–B/copy/Save placeholders. Slider release commits a seek; rapid requests supersede rather than queue. Close keeps media on disk in this intermediate increment.

- [x] RED headless tests for own chrome, physical/DPI placement, icons, initial pause, seek dispatch, async close ownership, startup failure and lifecycle cleanup. Integrate fake host into recording tests; never instantiate production decoder/device from fixtures.
- [x] Implement preview surface with reusable WriteableBitmap; marshal/copy frame while owned and reject late callbacks. Close stops playback before window/session release. Exit awaits preview cleanup before shutdown; user settings stay hidden/unavailable while result is owned.
- [x] GREEN targeted UI/ownership tests and full scripts/build.ps1.

## Task5: Review and source-only handoff

- [x] One independent source review of generation/buffer/process/audio/exit ownership. Material findings reproduced RED then fixed in one pass; full GREEN suite after fixes.
- [x] Update README/current-source notes, verification doc and named ledger with exact evidence and limits. Preserve0.4.1 archive hash. Native playback/A–V sync and multi-monitor presentation remain user acceptance.
- [x] Next increment: A–B Keep/Remove/Reset, immutable file clipboard, atomic final Save and recovery/cleanup UI; no portable feature-release claim yet.

Self-review: metadata feeds decoder and geometry; audio output feeds scheduler; all UI callbacks are generation guarded and bounded. Every review-focus item has a fixture owner. Implementation proceeds under the user's explicit continuation authorization.
