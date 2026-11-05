# WGC screenshots — internal verification, 0.12.0

- Locked restore and Release solution build: zero warnings/errors.
- All479 internal tests passed:121 Core and358 App. Log: `TestResults/wgc-stage1-build.log`.
- Native sessions, physical desktop composition, known-SDR policy and screenshot routing are connected in CompositionRoot. Video remains FFmpeg/GDI.
- Synthetic tests cover negative origins, portrait dimensions, padded rows, opaque alpha/black gaps, invalid/missing frames, temporal reacquisition, shared deadline, topology changes, future timestamps, cancellation and quick-copy/editor freshness.
- Pure color-policy tests cover SDR, HDR/advanced color, unknown metadata, absent monitors and cloned targets. CCD ABI sizes/offsets are checked without calling Windows.
- Review found Task.WhenAll could expose a fallback-eligible source error while another monitor failed with invalid data/OOM. Acquisition now inspects all faults and preserves non-fallback failures; two regression cases verify Automatic never calls GDI in that situation.
- Existing recording/audio/pause/edit, capture lifecycle, language, clipboard-format and history tests remain green. No real capture/device APIs were exercised.

Packaging uses self-contained publish, includes source/runtime assets/licenses, and excludes external FFmpeg and user settings/media. Archive evidence: `TestResults/wgc-stage1-package-check.json`.

Native acceptance remains pending: `WGC-stage1-manual.md`. Driver behavior, real topology/DPI/color metadata, exclusion and performance are not proven by synthetic tests. The cooperative deadline cannot forcibly interrupt synchronous driver/COM initialization or cleanup.
