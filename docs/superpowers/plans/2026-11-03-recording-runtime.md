# Recording runtime and right-edge editor layout

> Execute the UI/process/session tasks inline and delegate only the isolated WASAPI adapter. One final independent integration review.

**Goal:** Correct the reported wide right-edge toolbar and Copy/Save presentation, then deliver actual silent/audio region recording using the already approved settings and live-selection flow.

**Spec:** docs/superpowers/specs/2026-10-31-sharevibex-design.md and docs/ROADMAP.md stage 2. User explicitly requested implementation after these fixes; no new architecture approval loop.

## Constraints and choices

- English source/comments/UI. Fixed application-local data. No main window. Retain live selection and quick unedited Enter/Ctrl+C.
- User owns native desktop/device acceptance. Never launch the application, native capture harness, live clipboard or actual capture/audio devices. Internal/headless/synthetic tests and builds only.
- No Git metadata; reuse current workspace. Preserve older portable builds/data. No FFmpeg redistribution or automatic tool/driver installation.
- First Windows backend is FFmpeg gdigrab. Native audio adapter uses pinned NAudio.Wasapi 2.x (MIT) and a paced 48kHz stereo float pipe, including silence. Explicit DirectShow input remains optional.
- Screenshot panel width follows selected region (bounded useful minimum/maximum), rather than always760 logical pixels. Placement uses measured dimensions and monitor-local logical coordinates. Copy/Save use accessible vector icons.

## Tasks

1. Reproduce right-edge fixed-width behavior with geometry/headless regressions. Extract bounded region-relative toolbar sizing/placement and add vector Copy/Save; preserve actions/shortcuts.
2. Shared audio contracts/device IDs; isolated Windows audio factory with endpoint enumeration on explicit request, WASAPI playback/mic/mix, bounded buffers, paced silence and named-pipe cancellation/failure propagation. Pure tests only; no hardware calls.
3. Recording process and session coordinator: version/capability preflight; validated command arguments; startup readiness via FFmpeg progress; bounded diagnostics; asynchronous q-stop, mux completion and timeout/kill; temporary local output rename only on success; preserve recoverable partials on failure. Handle audio faults/cancellation/repeated Stop/early process exit.
4. Wire Video buttons from same region, choose local destination, hide overlays before process start, show elapsed/Stop controls and tray Stop + hotkey. No tools/controls in capture: hide/exclude recording window or use tray-only control if exclusion unsupported. Exit during recording offers Stop and save / Continue. No silent audio fallback. Settings can explicitly list native endpoints and select playback/mic; DirectShow requires user-provided device.
5. Full internal tests, source review, focused fixes, portable0.4.0 and manuals/licenses. Native frame accuracy, A/V sync, encoder initialization/device compatibility and mixed DPI remain user checks; do not claim these from unit tests.
