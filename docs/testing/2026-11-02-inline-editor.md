# In-place editor verification

The Nov 2 user correction replaces main-window startup and separate screenshot preview. The app starts in the tray, exposes Capture region / Settings / Exit, and keeps the region and annotation tools on the frozen desktop until explicit copy/save/cancel.

Research: ShareX RegionCaptureWindow, RegionCaptureToolbar, RegionSelectionOverlay, RegionCaptureOptions, and https://getsharex.com/docs/region-capture. QuickCapture=false is the equivalent persistent-selection mode; do not claim ShareX's default QuickCapture=true does this after mouse release.

New automated checks cover annotation history and redo branching, exact fill/export pixels, ellipse blur mask corners and effect ordering, and headless pointer selection that remains open for annotation, movement and resize. Source pixels remain immutable. Existing geometry/cancellation/atomic output and native capture checks remain in the suite.

Actual Windows native harness passed again on one monitor at 150%: exact desktop BGRA fixture, PNG saved/decoded under a Unicode/spaced path, real image clipboard round-trip, shortcut conflict and recovery. Initial startup inspection showed a shortcut conflict opened Settings automatically; corrected to tray status only before release.

Physical cross-monitor dragging, HDR/protected content and long-session performance remain unverified. A full-desktop preview is recomposed during drawing (throttled to about 30 updates/sec); performance on very large desktops needs measurement. Toolbar and dimming are display UI and are not included in exported pixels.

Release suite: 48 tests passed (30 core and 18 app), build zero warnings/errors. Independent review's two important findings (cross-monitor toolbar values and editing while an export dialog is active) were reproduced by failing tests and fixed. Moved-draft stacking was also reproduced and fixed so annotation preview preserves document ordering. Selecting an existing annotation loads its style before applying changes.

Known minor: clicking a selected annotation without moving it can add a no-op undo entry. Full native in-place editing and Save-dialog cancellation across multiple physical monitors were not exercised end-to-end by desktop automation; pointer interactions and persistent selection were exercised by headless Avalonia tests. Native pixel capture/PNG/clipboard were exercised separately on Windows.
