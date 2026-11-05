# Unreleased inline recording controls — increment 2

Implements the approved plan docs/superpowers/plans/2026-11-03-inline-recording-controls.md. Source-only intermediate increment; the portable 0.4.1 ZIP remains the independent FFmpeg filter-parser fix. Do not describe this increment as the complete requested recording/editor workflow.

## Connected behavior

- Video ready mode has Record, Sound, Microphone and Back vector icons with short tooltips/accessibility names. Source choices are independent and synchronized across monitor overlays.
- Record uses the selected physical rectangle and begins a session without a destination dialog. Overlay surfaces hide before handoff; no screenshot frame is acquired.
- Temporary capture, working file and recovery manifest are owned by data/recordings/<session-id>. Engine finalization still requires clean exit and nonempty output. Unexpected errors retain recoverable work.
- The small runtime window has Stop and Cancel icons plus media duration, encoded resolution, observed average bitrate and reported file size. Unknown metrics display an em dash; progress values come from FFmpeg, not a wall-clock estimate or bitrate target.
- Capture exclusion is attempted before showing controls. If unavailable, controls may be shown wholly outside the capture region; full-region cases with no safe placement fail before the recorder starts. Tray/hotkey Stop remain connected.
- Stop/natural completion retain one typed pending result. The interim result panel shows the retained file path; Close keeps its files and releases session ownership for another capture. This is a handoff for the later real preview, not a video player.
- Cancel stops/releases resources before deleting only this instance's known capture/working/manifest files. Manifest content cannot authorize arbitrary deletion. Unknown files and other sessions remain intact.
- Display topology changes stop the current recording and report the reason on the panel. Repeated commands and cancellation/startup transitions are serialized.

## Internal verification

All verification uses headless Avalonia, synthetic/fake recording sources, temporary test files and plain hidden console-process protocol tests. It covers fragmented/invalid/overflowed progress, source combinations, startup Stop, cancellation/retry ownership, natural/repeated completion, safe cleanup, right/bottom/negative-monitor geometry at100/150/200% scaling, exclusion-before-Show/refusal and simulated topology change. The normal screenshot regression suite remains included.

Final Release build passed221 internal tests (82core+139app), with0warnings/errors. One independent source review found two material cancellation/cleanup issues; both were reproduced with failing regressions before correction. Runtime tooltips are constrained to the same overlay surface instead of separate popup windows. Native exclusion remains unverified.

Two nonblocking review notes remain for the later result/presentation work: the compact failed-result panel can prefer a recoverable path over its full error explanation, and monitor removal does not explicitly reposition that panel onto another monitor. Stopping and tray availability remain connected; real monitor-removal accessibility needs Windows acceptance.

The agent does not launch the actual application, execute the system FFmpeg, enumerate/open native audio endpoints, capture the desktop/compositor, operate native UI or alter the live clipboard. Passing internal tests is not proof of capture exclusion, compositor/DPI behavior, real encoder/device operation or A/V sync.

## Remaining at the end of increment 2

Update: increment3 Pause/Resume and live microphone control is now implemented in source; see recording-pause-microphone-source.md beside this document for current evidence and limits. The list below preserves this earlier increment's handoff.

1. Segmented Pause/Resume excluding pause wall time, plus live microphone activation/release and a stable audio timeline.
2. Real video/audio preview in the same region, Play/Pause and generation-safe seeking.
3. Frame-aware A–B Keep/Remove/Reset, immutable edited video-file clipboard export, atomic final Save, cleanup/recovery and portable release packaging.

After those are connected, provide the user a runnable portable feature build and a focused real Windows acceptance checklist. The current source is not presented as a completed native recording acceptance result.
