# Encoder help validation — staged batch

FfmpegEncoderProbe queries only -hide_banner -h encoder=<exact name>. It verifies the real encoder header, reads typed AVOption rows, and checks private options actually emitted by FfmpegCommandBuilder. Numeric values are checked when help provides an explicit finite decimal from/to range. Unknown encoder output is rejected even with successful exit; prose does not establish support. Generic codec options (bitrate/global_quality/q:v) are not incorrectly required in private AVOption help.

The newly appended encoder implementations use this check during recording preflight before source resolution/audio/process startup, and through the default Test FFmpeg delegate. Existing encoders retain established capability checks for this staged rollout. Encoder changes invalidate the in-flight settings probe so stale responses cannot overwrite current status. Custom commands remain separate.

Six fake-runner cases cover stdout/stderr, supported options/ranges, older numeric limits, missing options, unknown encoder with exit0, and cancellation after query. No FFmpeg process, device or capture API was run. Symbolic numeric ranges and named enum/string values are not fully validated in this batch; no hardware or real-encoding guarantee is implied. Public archive remains0.12.3.

- Verification:529 tests passed (151 Core/378 App), locked restore/Release build zero warnings/errors. Log: TestResults/encoder-help-build.log.

## Enumerated private options

Help is now split into individual typed AVOption blocks. Constant names and numeric aliases belong only to their own option; a same-named constant in another block does not establish support. If a block lists enum constants, unlisted names/numbers are rejected; listed aliases also obey explicit numeric ranges. Options with no enum table retain numeric/static validation. This is still staged for newly added encoder IDs; legacy encoder help rollout, symbolic numeric bounds and free-form string validation remain pending.

Three added cases cover AMF quality/rc scope separation and listed numeric aliases. Full532-test suite passed (154 Core/378 App), locked restore/Release zero warnings/errors. Log: TestResults/encoder-enums-build.log. No native capture, encoding, device or GUI calls. Public archive remains0.12.3.

Reference: https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/amfenc_hevc.c

##0.13.0 established-encoder rollout

Removed the new-ID-only guard: all structured encoders now validate selected private help before source/audio/process startup and in the default Test FFmpeg action. Custom templates keep their explicit behavior. Synthetic engine fixtures explicitly supply encoder help; a missing required private option is verified to prevent opening audio or creating FFmpeg recording processes. Additional six core cases cover x264/x265, H.264/HEVC NVENC, H.264 QSV/AMF. No real encoder or device was executed.

Final0.13.0 suite:539 passed (160 Core/379 App), zero warnings/errors. Log: TestResults/encoder-legacy-help-build.log.
