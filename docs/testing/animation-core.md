# Animation core — first batch

Append-only encoder/container enums add GIF (gif), animated WebP (libwebp_anim/webp) and APNG (apng). Each has an exclusive allowed container/extension, output loop option, pixel format and encoder options. Settings include bounded loop count, WebP lossless flag and compression level. Core capture/assembly reject audio; edit graphs drop source audio. Current even-dimension padding stays consistent with the established scaling contract.

No animation choices are exposed in Video yet. Runtime lifecycle, no-audio controls, export/clipboard/history paths and preview remain required before release. Animated WebP encoding can buffer until finalization; direct first-packet readiness is not assumed to work. GIF currently uses a fixed RGB8 palette; adaptive palette/dither refinement is pending. APNG is lossless, WebP quality/lossless options are separate from video CRF/bitrate. Unused rate-control/preset/tune flags are not emitted.

Synthetic tests cover codec/mux/extension/pixel/loop arguments, audio rejection, no video-rate flags, segment assembly, lossless WebP, compression/loop bounds and edits with source audio stripped. No FFmpeg/device/native capture execution. Public portable remains0.13.1; this batch is source-only.

References: https://ffmpeg.org/ffmpeg-formats.html ; https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/libwebpenc_animencoder.c

- Verification:555 tests passed (173 Core/382 App), locked restore/Release build zero warnings/errors. TestResults/animation-core-build.log.
