# Animation export/preview boundary — internal batch

Animation extensions are recognized by derived files, edit restore metadata, clipboard cache ownership/cleanup and history. Save maps GIF/WebP/APNG to exclusive containers, validates before writing a destination, and preserves existing files on incompatible extension. Copy remains an immutable file and survives workspace disposal. APNG thumbnails use bounded FFmpeg extraction; GIF/WebP use image-codec first-frame thumbnails. Preview inputs pass ignore_loop1 before the input to avoid repeated embedded loops.

FfmpegAnimationProbe runs muxer/demuxer/decoder metadata queries before animation capture. GIF/APNG require matching readers/writers; animated WebP requires webp_anim demuxer/decoder, not merely static WebP support. FFV1 decoding is also required by final assembly. Encoder availability remains checked independently. These checks are prerequisites, not proof of native frame/timing correctness.

Injected export tests cover each format: A–B revision, immutable repeated Copy, compatible Save, rejection preserving an existing MP4, monthly History, edit restore and clipboard survival after workspace deletion. Preview-command tests check one-pass input ordering. Four fake probe cases cover compatible lists and static-only WebP rejection; no actual processes/media/device APIs run.

Full locked restore/Release suite:572 passed (178 Core/394 App), zero warnings/errors. Log: TestResults/animation-export-build.log. Source-only batch; public portable remains0.13.1. Animations stay hidden until settings and ready/runtime no-audio controls are complete.

Primary capability references: https://ffmpeg.org/doxygen/trunk/webp_8c_source.html ; https://www.ffmpeg.org/doxygen/trunk/webp__anim__dec_8c_source.html
