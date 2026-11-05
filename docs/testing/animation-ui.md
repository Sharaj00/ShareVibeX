#0.14.0 animation delivery

GIF, Animated WebP and APNG are exposed with exclusive containers; internal FFV1 remains hidden. Animation fields include loops, WebP quality/lossless and compression, restored per encoder. Audio/custom/rate controls are disabled or hidden; inactive rows do not leave labels behind. Ready/runtime sound and microphone cannot enable for animation; endpoint probes are skipped. Profiles predating animation fields retain defaults.

Metadata validation covers intermediate/final encoders, Matroska read/write and final animation mux/demux/decode. Review found omitted Matroska checks; a regression now rejects a missing intermediate reader before capture. No other confirmed review findings.

Locked restore/Release build:582 passed (180 Core/402 App), zero warnings/errors. Log: TestResults/animation-ui-build.log. English/Russian headless tests cover choices, container/rate restrictions, valid Save options, profile restoration and silent disabled controls. Existing lifecycle/export/ownership tests remain green.

No native capture, FFmpeg encoding/decoding, audio devices, clipboard or GPU APIs were executed. GIF fixed RGB8 palette is an initial quality limitation. Actual media timing, quality, long recordings, buffering and disk/RAM/CPU use require user verification; VERIFY.md documents acceptance. Public package0.14.0 is prepared through the existing portable publish workflow.
