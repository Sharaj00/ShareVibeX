# Video WGC — capability batch

Existing approved Stage2 design: docs/specs/2026-11-04-windows-graphics-capture/design.md.

FFmpeg probing now requests `-h filter=gfxcapture` only if the actual filter list contains gfxcapture. Option names must appear in typed help rows, not prose or version text. Checks require hmonitor, crop edges, cursor, maximum framerate, dimensions, output format, resize policy and hwdownload/format. The hmonitor option allows explicit native-monitor identity instead of assuming application monitor sorting equals FFmpeg indices.

Optional help failure preserves existing GDI capability results. User cancellation still propagates. Video Settings reports prerequisites as preparation only; it does not offer an unimplemented source. No capture command, process source selection, audio mapping or encoding behavior changed.

Fake-runner tests cover stdout/stderr help, absent options, missing transfer filter, failed optional help, GDI independence and no capture/device-list arguments. Real FFmpeg/GPU/device calls were not run. Next: monitor-relative source command construction, complete-region policy and session backend pinning before runtime integration.

Primary reference: https://ffmpeg.org/doxygen/8.1/vsrc__gfxcapture_8c_source.html

Verification: scripts/build.ps1 completed locked restore/Release build with zero warnings/errors;485 tests passed (126 Core/359 App), including cancellation during optional help. Log: TestResults/wgc-video-probe-build.log. No new public archive was published for this preparatory batch; portable release remains0.12.1.

## Region/source command batch

WgcVideoSource now builds immutable lavfi input graphs from physical desktop regions and native monitor handles. Single-monitor crops become monitor-relative edges. Multiple-monitor regions and gaps use independent BGRA hardware downloads plus RGB/straight-alpha overlays on a fixed black canvas; output frame rate is normalized. The existing output scaling/encoder/audio mapping follows this input, so dimensions are not scaled twice. Native handle numbers are formatted invariantly; no window titles, names or shell interpolation enter the graph.

WindowsWgcVideoSourceResolver maps only intersecting monitors, validates known SDR metadata and native handles, and rechecks topology. Custom templates remain explicit; the builder rejects managed source mismatch in region/fps/cursor or custom mode. Backend source filters/device requirements now include lavfi/setpts/fps and conditional color/overlay.

Synthetic command/resolver tests cover negative coordinates, odd dimensions, portrait/gap/multiple monitors, deterministic order, audio index1, output scaling, absent/duplicate handles, stale source/options, custom commands, topology and color rejection. No native enumeration, GPU, FFmpeg or capture process was executed. Runtime integration/source pinning/fallback and release are still pending.

Final source-batch verification:493 tests passed (131 Core/362 App), locked restore and Release build zero warnings/errors. Log: TestResults/wgc-video-source-build.log. Read-only graph review reported no confirmed findings; actual frame composition and A/V timing remain native acceptance.

## Engine/session integration batch

RecordingRequest carries an explicit method; its default stays GDI for legacy/internal callers. Engine validates encoding/command shape before native resolution, queries FFmpeg, resolves WGC only when requested, and falls back in Automatic only for typed missing source capabilities or resolver unavailable errors before opening audio/starting a process. Strict WGC fails explicitly. Invalid topology/data, allocation, cancellation, encoder/audio/output or generic process-start failures are not retried through GDI.

RecordingRun exposes effective method/fallback reason. Session accepts the initial preference and pins the actual method only after an accepted start. Resume sends a strict pinned request, including after failed resume/retry; a new session re-evaluates preference. Custom templates remain explicit and are never rewritten as gfxcapture. Managed-method reporting must distinguish custom commands at the future UI boundary.

This batch does not wire app preference/service/UI; the public archive remains0.12.1. Native FFmpeg capture/device APIs were not executed. Generic FFmpeg pre-first-frame errors are deliberately not parsed into source failures; Automatic is not a blanket retry for any FFmpeg startup failure.

- Engine/session batch verification:507 tests passed (131 Core/376 App), locked restore/Release build zero warnings/errors; read-only review no findings. Log: TestResults/wgc-video-runtime-build.log.

## Production wiring / intermediate0.12.2

CompositionRoot now injects WindowsWgcVideoSourceResolver; RecordingService snapshots the common CaptureBackend preference at session start. The shared selector/help applies to screenshots and structured video; Video removes the disabled GDI selector and points to Capture settings. Service status reports method/fallback and labels custom commands separately. Headless service integration verifies preference changes affect only new sessions. Restore clears stale source metadata.508 tests pass (131 Core/377 App), locked restore/Release build zero warnings/errors; TestResults/wgc-video-wiring-build.log. Native WGC video, A/V timing, topology and performance remain pending. Encoder expansion is not completed. This is a user-verification intermediate build.
