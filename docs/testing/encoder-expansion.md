# Encoder expansion — internal batch

New choices: HEVC AMF/QSV, VP8/VP9 libvpx, AV1 SVT/libaom, MPEG-4 Xvid. Enum values are appended to preserve existing numeric JSON. Current capability preflight requires the exact encoder implementation; no AV1 implementation or hardware encoder is silently substituted.

Commands share AddEncoding across capture, edits and segment assembly. AMF uses quality/constant-QP, QSV global_quality, VPx/AOM cpu-used plus CRF/bitrate, SVT numeric preset plus CRF/bitrate, Xvid q:v/bitrate. VP8 CRF is constrained by its visible target bitrate; VP9/AOM quality explicitly set b:v0. Software-only modes omit x264 presets/tunes. Quality bounds and numeric presets are encoder-specific; profile restoration updates bounds before values, preserving quality63 when returning from x264. VP8 requires Matroska. Inactive VP8 profiles do not constrain another active codec's container.

Synthetic tests cover all new quality/bitrate/assembly commands, enum compatibility, range/container errors, inactive profiles and headless cross-codec quality restoration. No actual FFmpeg encoding, hardware, capture or audio APIs were executed. Listed encoders do not establish working hardware or option availability in every FFmpeg version. Private encoder help/ranges, animation formats and native media acceptance remain pending; portable archive remains0.12.3.

References: https://ffmpeg.org/ffmpeg-codecs.html ; https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/amfenc_hevc.c ; https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/qsvenc.h

- Verification:523 tests passed (145 Core/378 App), locked restore/Release build zero warnings/errors. TestResults/encoder-expansion-build.log.
