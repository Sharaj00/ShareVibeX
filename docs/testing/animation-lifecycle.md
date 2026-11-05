# Animation lifecycle — internal batch

The existing session controller records lossless FFV1 level3/bgr0 Matroska segments for GIF/WebP/APNG, and retains desired final options independently. It normalizes audio choices to silent before applying sources, disables live microphone control, and preserves the selected capture backend across resumed segments. Intermediate and desired output encoders are checked before recording.

Stop converts every animation, including a single segment, through the existing assembler; MKV bytes are never merely copied to an animation extension. Assembly waits for process exit, so a final buffered encoder need not signal a first frame. Scaling happens at capture only. Intermediate files are owned by the same workspace/manifest and use normal cleanup.

Six injected-process lifecycle cases cover each format with/without pause. They verify silent lossless capture, final codec/extension, no copy shortcut, no audio-source access, and completion even when the final process never supplies Ready. A separate core regression removes invented100x100 geometry from encoder-help probing.

Locked restore/Release build: zero warnings/errors;562 tests passed (174 Core/388 App). Log: TestResults/animation-lifecycle-build.log. No actual FFmpeg/GPU/audio/capture calls. Temporary lossless recordings can consume substantial disk space; throughput and native media validity are not proven by synthetic tests.

Animation and internal FFV1 choices remain hidden. Next: preview/decode capability handling, animation file ownership/export/clipboard/history, sound controls and visible settings. Published portable remains0.13.1.

Reference: https://github.com/FFmpeg/FFmpeg/blob/master/libavcodec/ffv1enc.c
