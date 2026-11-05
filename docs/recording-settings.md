# Recording settings and runtime — 0.5.1

Video starts from the shared live region without a destination picker: Record, Sound, Microphone, Back. Runtime icons are Stop, Pause/Resume, Microphone, Cancel, at the ready toolbar anchor with matching 40px icons, media duration, dimensions, bitrate and stored bytes. A dashed capture outline uses only thin excluded edge windows. Live statistics prefer observed progress/file length; unusable header-only counters fall back to labeled ≈ estimates from scaled output dimensions/fps/quality or configured bitrate. CRF estimates are content-dependent, while finalized size remains actual. Controls are capture-excluded or wholly outside the region; otherwise presentation refuses startup. Stop enters in-place A/V playback, A–B editing and export. Actual capture/encoder/device acceptance is user-owned; see testing/2026-11-03-manual-0.5.1.md.

Settings stays compact: Encoding video/audio columns, Scaling, Devices and command Preview tabs. FFmpeg path/test and additional/custom options stay accessible. Native endpoints enumerate only on explicit Refresh. Settings cannot change while a recording/preview/export owns the session.

## FFmpeg discovery and test

Discovery order is an explicit override, system PATH, known Program Files ffmpeg/bin locations on Windows, then `tools/ffmpeg.exe` or `tools/ffmpeg/bin/ffmpeg.exe` beside ShareVibeX. An explicit relative override is resolved from the application folder; a missing override is an error rather than silently using a different executable. File discovery is not capability validation.

Test FFmpeg runs `-version`, `-devices`, `-encoders` and `-filters` only. It does not list/open physical devices or capture frames/sound. Each query has an eight-second timeout and a one-megacharacter output limit per stream. Changing the path or closing Settings cancels the old query and prevents stale status from appearing. A listed NVENC/QSV/AMF encoder does not establish that compatible hardware exists or can initialize. Custom templates need separate runtime validation.

FFmpeg is not installed or distributed by this application. A missing FFmpeg does not disable screenshots or prevent saving other settings.

## Normal command mode

The first candidate video backend is Windows GDI desktop capture (`gdigrab`) using physical coordinates. Frame rate, cursor and selected capture dimensions belong to the input. Output scale/padding and encoder options belong to the output. Negative X/Y values are preserved.

Defaults: MP4, libx264, 30 fps, quality 23, auto preset (veryfast), cursor enabled, no duration limit, original size, system audio as the default Sound source. GIF, Animated WebP and APNG are silent and select their container automatically. GIF quality uses a per-frame palette with a configurable color limit (4–256) and Sierra, Bayer or no dithering. H.264 MP4 is the default sharing option, but application/service acceptance must be tested on actual output on the real target machine.

Available encoder choices are libx264, libx265, h264_nvenc, hevc_nvenc, h264_qsv and h264_amf. Software uses CRF, NVENC CQ/VBR, QSV global quality, and AMF constant QP in quality mode. Bitrate mode uses the target video bitrate. Preset/tune choices depend on the selected encoder. The installed FFmpeg version and hardware may impose further constraints; this tranche does not claim these combinations have been runtime-tested.

Native system/microphone/mixed audio uses a48kHz stereo float pipe. Devices > Refresh lists active Windows endpoints only on request; defaults use Windows Multimedia endpoints. An explicit missing endpoint is an error, not a fallback. Mono/stereo PCM16/24/32 and float32 are supported; surround must be configured as stereo. Streaming linear resampling/mixing, bounded buffers, paced silence and source/pipe errors are handled outside the UI thread. Native latency and long-run A/V synchronization still require device tests. DirectShow uses an exact user-entered installed device name. MP4 uses AAC; Opus requires Matroska/48000Hz output in this UI.

## Scaling

- Original: keep content pixels, pad the right/bottom edge if a dimension is odd.
- Percentage: 50 gives half width and half height.
- DivideBy: 1.5 gives width/1.5 and height/1.5.
- TargetSize: set one dimension with the other zero to derive it when aspect ratio is locked. With both positive, fit inside the box. Unlock aspect ratio to stretch to both dimensions.

Content dimensions round to the nearest pixel; the final frame pads to even dimensions. The ordered filter pipeline scales if requested, pads, then converts to yuv420p. Capture coordinates and source dimensions remain independent. The preview uses 1920×1080 at (0,0); actual region-dependent validation will run again before recording.

## Additional arguments

Normal mode accepts option/value pairs from this explicit list: `-g`, `-keyint_min`, `-bf`, `-refs`, `-maxrate`, `-bufsize`, `-profile:v`, `-level:v`, `-threads`, `-metadata`.

Example: `-g 60 -maxrate 12000k -bufsize 24000k -metadata "title=My clip"`.

Input, filter, codec, audio enable/disable, container and output options are managed and cannot be overridden here. Use custom mode for an explicitly supported alternative. Parsing supports quoted values; Windows backslashes, UNC paths and escaped quotes are preserved. Execution uses an argument list without a shell; the displayed string is a preview, not a shell script.

## Custom template

Custom mode replaces normal input/video encoding options. Structured encoder, rate-control, quality, preset, tune and additional arguments are disabled and ignored. Container/final destination remain managed. The template is parsed with a restricted option vocabulary rather than accepting every possible FFmpeg command.

| Placeholder | Meaning |
| --- | --- |
| `$fps$` | Selected frame rate |
| `$area_x$`, `$area_y$` | Physical desktop origin |
| `$area_width$`, `$area_height$` | Physical capture size |
| `$cursor$` | 1 or 0 |
| `$scale_filter$` | Ordered scale/pad/pixel-format filter string |
| `$duration$` | Positive time limit in seconds; unavailable when zero |
| `$audio_input$` | Standalone argument-list fragment for the selected audio source, empty in silent mode |
| `$audio_options$` | Standalone output audio fragment, or `-an` in silent mode |
| `$output$` | Exactly one standalone final destination; runtime supplies a unique temporary output path |

Unknown placeholders, malformed quotes, unsupported option names, multiple positional outputs and multi-output muxers are rejected. The template must work for both audio modes to save. Silent mode requires output `-an` after the last input and rejects audio encoding/filter options. Audio mode requires one video input followed by exactly one `$audio_input$`, then output `-map 1:a:0` and `-c:a` (normally supplied by `$audio_options$`). Non-original scaling requires `$scale_filter$` in an output `-vf` value after the last input. A positive duration requires exactly one output `-t $duration$` after the last input; mentioning it in metadata or before an input does not apply the selected limit. A single ordered `-vf` is supported; filter_complex and arbitrary muxer/filter source combinations are outside this tranche's validated vocabulary.

The audio pipe and output filename in Settings are examples. Runtime rebuilds the command for the actual region and pipe, validates capabilities and starts FFmpeg only after Record. Hardware failure does not silently change the encoder; choose H.264/x264 in settings for a software fallback. Custom filters can change output dimensions beyond the configured scaling estimate.

## Lifecycle and errors

First-frame progress confirms startup, with a12-second startup/setup deadline. Stop sends q through stdin and waits for muxing; a15-second process deadline falls back to termination. Recording segments finalize inside the owned session; a chosen destination is only touched by the later Save action. Errors retain a nonempty partial and its location; partials are not claimed as valid finished videos. Native device setup timeout/cancellation disposes late sources. Normal FFmpeg audio-input closure during Stop/duration is distinguished from a device failure, and is accepted only with a clean bounded process exit.

Settings stay closed during recording. A hidden exit confirmation is shown only if Windows capture exclusion can be configured for it; otherwise use tray Stop before Exit. Windows job ownership ends the FFmpeg child if ShareVibeX exits unexpectedly. User verification must check actual capture exclusion, input timing, hotkey/tray Stop, repeated sessions and recovery.

## Pause, preview and exports

Pause finalizes a segment and releases devices; Resume reopens the selected sources into the next segment. Duration limits apply to cumulative media time. Native segments use a stable audio timeline with silence for disabled sources; a completely silent final recording omits its track. Microphone enabling opens the device, disabling releases it. Failures do not silently become silence. Custom capture templates do not support Pause, and legacy DirectShow cannot mix a native microphone.

Stop normalizes/joins segments. Preview is first-frame paused, inside the original physical region with letterboxing, capped at 960×540/30fps. FFmpeg video/audio decoding is bounded and separate; NAudio output supplies the audible clock. The unified seek/playhead/A–B bar appears inside the video on hover (Tab for keyboard access); adjacent icon controls remain transparent. Previous/Next frame pauses and steps at original output fps. Seeking drains obsolete jobs and rejects stale frames. The original stays immutable.

A–B markers snap to output frames. Keep retains [A,B); Remove retains the complement; repeated edits map to original source intervals. Reset restores the stopped capture. Edits and cross-container exports re-encode using structured encoder/audio options, not the custom capture template. Capture scaling has already been applied and is not applied a second time. No silent hardware-to-software fallback. If final duration is unknown, A–B editing is unavailable; original-container Copy/Save remains possible. Oversized edit command graphs fail explicitly with the source retained.

Copy publishes an immutable file through Unicode CF_HDROP with copy semantics. Files live under data/clipboard until explicit confirmed cleanup in tray Retained recordings; they survive preview/app closure. The receiving app determines file/video/animation behavior. Save uses a fresh sibling temporary file and commits only after successful nonempty output; cancellation/failure keeps the old destination. Copy/Save keeps preview open. Unexported Close/Exit offers Save/Discard/Continue. Playback/export drains before known-file session cleanup.

Finalized supported recovery metadata can reopen from the tray, including validated edits. Interrupted media and older metadata without a final audio flag remain manual-only; no automatic repair. Session cleanup never recursively removes unknown files or copied/saved exports. Explicit cache cleanup invalidates existing paste/history references.

## Sources


- [FFmpeg input devices](https://ffmpeg.org/ffmpeg-devices.html), including gdigrab and DirectShow.
- [FFmpeg command options](https://ffmpeg.org/ffmpeg.html), including input/output option ordering and capability queries.
- [FFmpeg filters](https://ffmpeg.org/ffmpeg-filters.html), including scale, pad and format.



##0.13 encoder profiles and preflight

The common capture method is selected in Video and applies to screenshots/structured video. Available encoders include x264/x265, H.264/HEVC NVENC/AMF/QSV, libvpx VP8/VP9, libsvtav1/libaom-av1 and libxvid. VP8 requires Matroska. Per-encoder encoding fields persist separately; switching restores them without changing shared capture/audio/scale/container preferences.

Test and structured startup also query -h encoder=<selected implementation>. Typed private option names, explicit finite numeric bounds and enumerated constants are validated before source/audio/process initialization. Generic FFmpeg options, symbolic ranges and unrestricted strings are not fully described by private encoder help; real encoding/hardware remains user acceptance.
