# Real process suspension spike — 2026-11-03

Scope explicitly selected by the user: test genuine process suspension before implementing a prepared recorder. The experiment used the installed FFmpeg child with generated 160×90/10fps testsrc2 video and 48kHz sine audio, no desktop, microphone, playback device or ShareVibeX GUI. Only children started by the experiment were suspended/resumed. Output inspection used the matching ffprobe. Synthetic testing is not native gdigrab/audio-device acceptance.

## Result

NtSuspendProcess / NtResumeProcess successfully stopped and resumed the child. During a 2.018-second suspension its CPU-time delta was zero. That did not produce a media-aware pause:

| Run | Wall time | Actual suspended time | Video duration | Audio duration | Video/audio duration difference |
|---|---:|---:|---:|---:|---:|
| Control, no suspension | 5.007s | 0s | 5.200s | 5.163s | 0.037s |
| Same synthetic CFR inputs, suspension | 4.779s | 2.018s | 4.900s | 3.072s | 1.828s |

Active wall time in the suspended case was 2.761s; video retained approximately the whole elapsed timeline rather than excluding the wait. Max CFR video frame timestamp spacing was 0.1s, so the timestamp gap was filled by CFR instead of representing a clean pause. An earlier passthrough experiment exposed a 1.810s timestamp gap, but its synthetic wall-clock source also burst frames; it is exploratory evidence, not a production benchmark.

The raw successful comparison is retained locally under .superpowers/spikes/process-pause/7775cef9a562485e9ca916a569c0e713/result.json with matching streams/diagnostics and synthetic MP4s. Its baseline PauseSeconds/ActiveWallSeconds field names are legacy script labels for an observation window: the baseline was NOT suspended. The table above corrects that naming. The script and raw data are experimental and are not part of the portable runtime.

## Interpretation and decision

Direct OS process suspension alone is unsuitable as the prepared-recorder implementation. Stock FFmpeg has no media-pause stdin command; gdigrab timestamps packets with av_gettime, so its wall clock continues while threads are suspended. In ShareVibeX the separate native-audio producer would also need controlled backpressure, discard/drain and clock treatment. Do not expose a Prepare recording checkbox that claims the proposed paused behavior works.

A production solution needs explicit coordinated input/clock gating or a supported staged recorder lifecycle, with A/V sample ownership and timestamps defined before Record. The user selected testing true process pause first rather than background recording, so continuous pre-roll has not been substituted. No prepared-capture option was enabled in this patch. Further implementation needs a separate design based on these results.

Primary sources:
- https://github.com/FFmpeg/FFmpeg/blob/master/fftools/ffmpeg.c — check_keyboard_interaction.
- https://github.com/FFmpeg/FFmpeg/blob/master/libavdevice/gdigrab.c — gdigrab_read_packet sets pkt->pts from av_gettime.
- https://github.com/winsiderss/phnt/blob/master/ntpsapi.h — process suspension declarations.
