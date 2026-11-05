# In-place recording, playback and A–B editing

Status: approved by the user on 2026-11-03; implement in separate parts.

## Intent and agreed choices

Replace the destination-first recording workflow with a single region session: choose a region, configure sound/microphone, record immediately, stop into an in-place preview, optionally remove/keep an A–B interval, then copy or save. All action buttons use vector icons, short English tooltips and accessible names. The main application remains tray-only. The user owns real desktop, clipboard, audio and FFmpeg acceptance checks.

The user explicitly chose copying the finished video as a Windows clipboard file, and chose existing FFmpeg plus NAudio for preview rather than adding a separate playback engine. The original request says three icons but enumerates four; implement the four named actions: Record, Sound, Microphone, Back.

## Approaches

1. **Selected by the user:** external FFmpeg decoding/transcoding, Avalonia video surfaces and the existing NAudio audio layer. Reuses the configured executable and avoids another player distribution. Requires explicit playback synchronization, bounded buffers, cancellation and seek handling.
2. Separate playback engine such as LibVLC/mpv. More playback behavior comes ready-made, but adds binary discovery/distribution, licenses and portable-size/platform integration. Not selected.

Builder decisions below are proposals, not additional user-approved requirements.

## Session and interface

States: Choosing → VideoReady → Starting → Recording ↔ Paused → Finalizing → Preview → Exporting → Preview/Closed. Cancellation and failure transitions must be defined from every busy state. Only one session may own capture, playback or export resources at once.

- **VideoReady:** four icons: Record, Sound toggle, Microphone toggle, Back. Sound means system playback, independently of the microphone. Proposed defaults are system sound on, microphone off, initialized from existing audio-source preferences where possible. Both off means silent export. Device selectors remain in Settings. No Save dialog appears before capture.
- **Starting:** disable repeated commands, show a small starting indicator, keep Cancel available. Capability/audio/first-frame failure restores the same live region and source choices with a concise error.
- **Recording:** Stop, Pause, Microphone toggle, Cancel. Show recorded duration, output resolution, observed average bitrate and current stored bytes. Bitrate is measured from encoded bytes/media duration, not the configured target. Unavailable values use an em dash. The selected capture rectangle is fixed during recording. System sound choice stays fixed; microphone can change live.
- **Paused:** Stop, Resume, Microphone toggle, Cancel. Media duration stops advancing; wall-clock pause time is excluded from output. Changing microphone applies to the next resumed samples.
- **Preview:** display the first frame paused inside the original physical region, preserving aspect ratio with letterboxing. Provide Play/Pause, seek timeline, A/B markers, Keep A–B, Remove A–B, Reset edits, Copy video file, Save and Close/Discard icons. Timeline positions and A/B values are readable text; actions remain icons. Preview audio follows the current edit; no separate player window.
- **Exporting:** show progress and Cancel; prevent concurrent export/seek/edit actions that would mutate the export snapshot. Cancel preserves the preview and original capture.

Keep the control/status panel adjacent to the region using monitor-local DPI-aware placement, below/above when space permits. No dim/frozen full-desktop overlay while recording. A small independent topmost control window must be excluded from Windows capture before showing it; do not reuse a full-monitor overlay that intercepts the desktop. Exclude borders/indicators too. If capture exclusion is unavailable, place controls wholly outside the captured rectangle when possible; otherwise refuse that presentation with an actionable explanation rather than record the controls. Preserve tray/hotkey Stop as fallback. Display removal/scale changes stop safely and report the condition.

Preview owns the original region but does not dim the rest of the desktop. Clicking outside the preview passes through to other apps. A new capture is unavailable until this session closes; Settings remains unavailable during capture/playback/export ownership. Stop via tray or capture hotkey opens the same preview.

## Temporary storage, pause and cancellation

Store session-owned segments, a small recovery manifest and derived outputs under data/recordings/<session-id>. Record immediately to a unique managed path. Existing final destination files remain untouched until an explicit successful Save. A fully encoded, flushed, nonempty output must be verified before it is exposed as complete.

Proposed pause strategy: gracefully finalize a segment on Pause, resume into a new segment with identical video/audio parameters, and assemble the ordered media on Stop. Do not suspend the process: native capture/audio clocks and bounded pipes must not accumulate paused data. Serialize Pause/Resume/Stop and show the transition while flushing. Resume failures keep earlier segments recoverable. Stop while paused finalizes without creating an empty segment. Configured duration limits apply to the sum of recorded media, not independently to each segment. Custom capture arguments remain subject to existing managed-output restrictions; incompatible segment parameters fail explicitly.

Cancel means stop, release all resources and discard only the active session's owned temporary capture files. Do not delete copied clipboard exports or previously saved files. Unexpected process/device/disk failure retains usable segments and reports a recovery path. A malformed or abandoned manifest must never authorize deletion outside its session directory. App exit must gracefully stop an active run and offer Save/Discard/Continue for an unsaved preview; crash recovery can expose the retained manifest on the next launch.

## Audio and microphone controls

Extend the current paced 48 kHz stereo pipe/mixer with independent system and microphone state. Use a consistent audio stream across internal segments, inserting silence for disabled inputs so live microphone activation does not change stream topology. Open microphone hardware only when enabled, stop/release it when disabled, and do not keep recording it behind a mute icon. A failed enable leaves the previous state and recording intact, with a concise error. Existing active device failures stop safely without silently substituting silence.

If neither source was enabled throughout recorded media, final exports omit the audio track for GIF-style MP4 sharing. Otherwise silent intervals remain on the common timeline. On Pause release capture resources; Resume reopens the selected endpoints. Preserve existing explicit mono/stereo format limitations and selected endpoint IDs; no device enumeration merely from opening capture.

For legacy DirectShow selections, the Sound toggle controls that configured FFmpeg audio source instead of native playback; label its tooltip accordingly. A native microphone can be mixed only through an explicit compatible combined pipeline. If that pipeline cannot be supported in the first increment, report the incompatibility before capture and require choosing a native source; never silently ignore the microphone toggle or replace DirectShow.

## Playback and metadata

Use FFmpeg subprocesses with shell-free arguments and application-owned lifetime jobs. Decode bounded video frames into Avalonia bitmaps and PCM into an NAudio playback adapter. The audio playback clock leads when audio exists, otherwise a monotonic clock. Drop late video frames; do not accumulate unbounded frames/audio. Seeking cancels and drains the previous decoder, increments a generation and rejects stale output before restarting at the requested position. Stop all playback before editing/exporting or closing the preview.

This preview accepts this session's recordings only, not arbitrary imported files. Track encoded dimensions, frame rate, final frame count and media timestamps from FFmpeg progress for each segment. Normalize segment timestamps during assembly and reconcile final metadata before preview. Do not add an implicit ffprobe dependency: if the builder needs ffprobe for robust metadata validation, document and surface it as an explicit companion-tool requirement before implementation rather than silently fail without it. Decoder output dimensions are explicitly constrained to the preview surface; do not assume raw output matches custom capture filters.

## A–B editing and exports

A and B define an ordered nonempty interval in the currently displayed timeline, bounded to available media and snapped to the selected output frame grid. Keep retains [A,B); Remove retains [0,A) and [B,end). Reject Remove if it would leave no video. Seek and playback use the resulting timeline, not hidden source time. Repeated edits compose against the current timeline; Reset returns to the original stopped recording. Keep the original capture immutable. Marker defaults cover the entire current timeline.

Perform cuts by re-encoding affected output through FFmpeg with synchronized video/audio trimming and timestamp reset, rather than claiming frame-accurate stream-copy cuts. Join kept intervals with compatible parameters, including silence where required. Build the cut graph from a validated edit model; never interpolate user-provided command text. Reuse selected output container/encoder settings where compatible. Estimate/progress and cancellation are visible for longer operations. Do not silently replace a failed hardware encoder with software.

Copy renders the current edited version to an immutable file under data/clipboard, then publishes a Windows file-drop copy operation. Keep clipboard files after preview closure and app exit so pasted references remain usable; do not remove them when discarding session files. Proposed first version retains these files until the user explicitly clears that cache; no automatic expiry promises. Repeated Copy without an edit reuses the same immutable export. No GIF conversion is implied. Telegram and other applications choose how they ingest the file; sending as animation is not guaranteed by the clipboard protocol.

Save prompts for a destination after stopping/editing. Render to a unique temporary file beside the chosen destination, then replace only after successful completion. Cancelling the dialog/export preserves the preview and any existing destination. Successful Copy/Save keeps the preview open until Close, allowing another save or edit.

## Boundaries and staged implementation


Deliver reviewable increments, without labeling disabled controls as implemented functionality:

1. Independent 0.4.1 fix for old/new FFmpeg filter-list parsing and real missing-capability diagnostics.
2. Session storage/state, four ready icons, immediate start, excluded runtime controls, metrics, Stop/Cancel and preview ownership handoff.
3. Segmented Pause/Resume and live microphone control with stable audio timeline.
4. In-place decoded A/V playback and seek; immutable source and timeline model.
5. A–B Keep/Remove/Reset, copy-file and atomic Save; cleanup/recovery, documentation and portable packaging.

After approval, write the detailed implementation plan before product code for increments 2–5. Native acceptance remains the user's responsibility; internal tests cannot mark these scenarios accepted.

## Done-when scenarios

- The reported FFmpeg filter output is accepted; a genuinely absent filter remains an error.
- Record starts without a destination dialog; selected system/mic combinations are honored, including both off.
- Duration, encoded dimensions, observed bitrate and bytes update; controls and status never appear in the captured content.
- Pause excludes paused wall time; Resume repeats correctly, including Stop during pause and microphone changes/failure.
- Stop opens the in-place preview; play/seek remain synchronized, and stale frames do not appear after rapid seeking.
- Keep/Remove intervals produce correct frame/audio content, repeat edits and Reset work, empty output is rejected.
- Copy publishes the edited immutable video file; it remains pasteable after closing preview/app. Save cancellation/failure preserves both preview and old destination.
- Cancel/exit/error/repeated commands release devices and child processes, preserve recoverable work appropriately, and never delete unrelated files.
- Internal fixture/state/process/audio/geometry tests are reported separately from real Windows DPI, capture exclusion, encoder, A/V sync, clipboard and playback checks.
