# Approved history/theme refinement — 0.11.1

- [x] Reconcile filesystem history after every file action, independent of recycle boolean/error; reproduce disappeared-file false/error cases.
- [x] Cancel/drain thumbnail readers before file mutations and refresh on visible Settings/History return. Coalesce scans, stop work while hidden.
- [x] Remove Refresh and recording recovery UI/events/window, preserve existing files, internal metadata and damaged-settings recovery.
- [x] Remove tray History menu/choice; migrate legacy string/numeric OpenHistory actions to Settings without losing preferences.
- [x] Flatten rows to one transparent activation area plus red cross, no outer card.
- [x] Scope capture-action styling to explicit buttons; AMOLED translucent black, active accent outline and dynamic System fallback. Preserve palette and ordinary Settings buttons.
- [x] Release build0 warnings/errors;410 tests passed, additional disabled-state check passed. Independent read-only reviews completed.
- [x] Portable package/source/archive consistency verified: ZIP integrity, three shipped DLL hashes, nested source/tracking files, current manual/version, no FFmpeg or portable settings; SHA256 sidecar generated.

Native desktop/Shell/Recycle Bin/audio acceptance remains user-owned. Do not weaken recycle-only protection to hide stale rows. No data cleanup or tool/dependency changes are part of this request.
