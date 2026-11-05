# Recording UI refinement — 0.5.1

User confirmed 0.5.0 works, requests clear UI corrections. Implement those directly using Superpowers inline/TDD; real native acceptance remains assigned to user. No app/FFmpeg/audio/live clipboard launches. English source/comments/UI. Preserve prior archives and user data; no Git repo.

Design: share transparent capture-toolbar surfaces and 40px icon slots. Pass physical toolbar origin and logical size from ready mode to recorder; keep first row and microphone slot unchanged. Recording status below buttons, no new background or heading. Maintain thin dashed region-edge windows outside the captured pixels, no full desktop overlay/interior input interception; exclude each before showing.

Preview: put one hover/focus-revealed seek bar with A/B handles and playhead inside the video. Bottom lane seeks; top handles adjust A/B. Keep/Remove/Reset/export controls stay in an adjacent transparent icon toolbar. Add previous/next frame at original output fps, pausing first. Keep existing edit/export/close cancellation/resource contracts.

Metrics: prefer observed progress bytes and known working-file length; very small/header-only counts are not useful live sizes. Fall back to an explicitly approximate bitrate/size from configured bitrate or a pixel/fps/quality heuristic using scaled output dimensions. Actual final file size always wins. Do not claim CRF estimates predict content accurately.

Movement feasibility only: gdigrab initializes clip_rect from offset_x/y in read_header and does not expose a live offset command (FFmpeg libavdevice/gdigrab.c). Moving through segment restarts could introduce gaps; continuous whole-desktop capture + dynamic crop costs capture bandwidth and needs separate validation. No misleading drag affordance that merely moves the drawn border. Keep recording rectangle fixed in this patch.

1. RED/GREEN metrics sampling/estimate tests; no actual tool/device calls.
2. RED/GREEN transparent consistent toolbar geometry and handoff, dashed border ownership/exclusion/lifetime tests. Preserve capture safety fallback.
3. RED/GREEN unified hover timeline, A/B/seek interactions, frame stepping and old edit/export/close regression tests. Responsive control sizing.
4. Full internal suite, one narrow independent review, fix material findings with regressions, 0.5.1 local portable release, source/license/manual/package verification. Record native checks as pending.
