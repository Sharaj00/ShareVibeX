# Stage 1 Windows verification

## Automated verification

Initial RED → GREEN development runs:

- Portable settings: 8 tests. Defaults, theme/Unicode round-trip, corrupt/null/future/invalid-theme settings preservation, locked destination preservation, cancellation.
- Physical geometry/session: 10 tests. Reverse drag, negative desktop origin, right/bottom clipping, empty and out-of-bounds regions, one active session, cancellation and late completion rejection.
- Hotkey parsing: 10 tests. Supported modifiers/keys and invalid/unmodified/duplicate combinations.
- Workflow: 4 tests. Overlapping requests, stale snapshots after cancellation, selection cancellation, changed display topology.
- Export: 5 tests. Exact physical crop pixels, out-of-bounds rejection, PNG decode/color/dimensions, Unicode destination, failed and cancelled save preservation.

After review, 4 more lifecycle cases verify restoration of owned settings windows, tray close behavior, and allowing OS/application shutdown. All 41 tests passed together in Release with a zero-warning/zero-error build.

## Actual interactive Windows checks

- Main window and Settings opened with the Dark theme and readable controls.
- Existing ShareX occupied Ctrl+PrintScreen: clear conflict message; application still opened and exposed capture/settings controls.
- After the user exited ShareX and Settings was saved again, Ctrl+PrintScreen invoked the common capture workflow. A selected 589×350 region opened in the screenshot result window.
- Selection windows without taskbar entries were omitted by the automation helper's window list. Initial inference of a capture deadlock was incorrect; boundary diagnostics showed snapshot and window Show completed, and the result window was subsequently observed.
- PNG Save As dialog opened from the result window. End-to-end saving through that dialog was interrupted by physical window interaction, so this particular UI sequence is not counted as verified. Actual PNG writes are verified by the native harness below.
- Separate Release native harness passed on one physical monitor: known RGB(23,137,211) fixture matched actual captured BGRA pixels; 20×20 PNG saved and decoded under a Unicode/spaced filename; image copied to and read from the real Windows clipboard with matching dimensions/color; duplicate native hotkey registration failed and registration succeeded after release.
- Final native repeat passed at 150% display scaling. A prior repeat selected pixels underneath pointer-highlight software, producing a blue gradient rather than the fixture's flat color; the harness now selects a probe far from the actual pointer and still asserts exact pixels. This was a test-environment overlay, not PNG color corruption.
- Independent code review found owned-window restoration and OS shutdown cancellation bugs. Both were reproduced by failing tests and fixed; the complete suite passed afterward.
- Self-contained ZIP was extracted into a separate workspace path containing spaces and launched successfully, with its own portable settings and no optional tools. Light, Dark and AMOLED were visually checked; AMOLED saved as `Theme: Amoled` and applied to the main window. Alt+F4 hid the main window while the tray process remained running; that test process was then stopped and its shortcut released.
- One idle snapshot after theme switching: working set 169,402,368 bytes (~162 MiB), private memory 212,893,696 bytes (~203 MiB). This is a single baseline, not a long-session resource test; startup duration was not precisely measured.

## Remaining hardware/manual checks

- Multiple physical monitors, negative desktop origin and mixed 100%/150% scaling during an actual cross-monitor drag.
- HDR-to-SDR appearance and protected/exclusive fullscreen content.
- Physical monitor removal during a drag; automated topology-change cancellation is covered.
- Resource trends during a long capture session and clipboard behavior after exiting the entire process.
- Main-window show currently reapplies saved settings; an unsaved theme preview can reset when returning from capture. This is a known minor item for the next UI iteration.

