# Unreleased in-place video preview — increment 4

This source-only increment continues the approved recording workflow. The published0.4.1 portable ZIP remains unchanged; A–B editing, clipboard video-file export, final Save and recovery UI are next.

## Connected behavior

- Stop/natural completion opens a video surface on the selected physical rectangle, with own chrome and a nearby compact control strip. There is no main/player window or full-desktop dim layer. Outside the preview surface/strip, the desktop remains interactive.
- The first decoded frame is paused. Controls are Play/Pause and Close vector icons with short tooltips, readable position/duration and a seek slider. Space toggles playback; Escape closes. Slider release and keyboard navigation commit a seek.
- Preview decodes through the configured/discovered FFmpeg and plays audible output through existing NAudio/WASAPI. There is no additional player distribution or implicit ffprobe requirement. Paused thumbnail/seek and silent video do not open audio output.
- Decoder output is constrained to at most960×540 at30fps and letterboxed to preserve image aspect ratio. Original recording pixels/frame rate/file stay unchanged. Two independent decoder processes are used for audible video; this can cost more CPU than a unified player engine.
- Frames use pooled memory and bounded raw streams; UI copies a still-owned frame into a reusable bitmap and rejects obsolete generations. Rapid seek requests cancel old work before queuing the latest request. Playback follows the audio clock when present, a monotonic clock otherwise; late frames are dropped instead of filling a growing dispatcher queue.
- PCM queues are bounded, aligned float stereo48kHz with backpressure and prebuffering. Device/output/decoder failure is reported instead of silently falling back to muted playback. Source files remain available after failure.
- Close awaits playback/decoder/audio cleanup before releasing the pending recording session. Settings remain hidden/unavailable during result ownership. App exit awaits preview cleanup before shutdown. Closing this intermediate preview retains its files under data/recordings; final Save/discard/cache management comes next.

## Internal verification

Fixture coverage includes fragmented/truncated frames, frame bounds and decoder arguments, PCM ordering/backpressure/alignment/cancellation, paused first frame, silent/audio playback paths, replay/pause, superseded late decoder opens, stale results, stalled-video/audio failure, safe position reads, physical-region geometry at100/150/200% scaling, icon controls, headless bitmap rendering/seek/close and Stop-to-preview ownership handoff.

Final Release verification passed262 internal tests (82core+180app),0warnings/errors. One independent source review found canceled setup escaping ownership and completion waits ignoring cancellation. Both were reproduced with failing fixtures and corrected; old setup is now asynchronously drained before replacement/close, and EOF completion waits can be canceled. Slow native setup may delay an operation while leaving the UI thread unblocked.

One minor placement item is deferred: if the scaled control strip is wider than its monitor, the current null-placement path leaves default positioning instead of choosing an explicitly compact layout. Actual tiny-display/native DPI accessibility remains unaccepted.

No actual app, system FFmpeg, native screen/compositor capture, device enumeration/playback or live clipboard has been run by the agent. Internal tests/static source review do not establish real mux timing, audible A/V sync, custom-command output metadata, driver behavior, rendering performance or mixed-DPI/native compositor presentation. These remain user acceptance checks after the complete feature build is packaged.

The preview accepts this session's finalized output and known recording metadata, not arbitrary imported media. Decode bounds and metadata assumptions are explicit; they are not a claim that every external file/codec has been validated.
