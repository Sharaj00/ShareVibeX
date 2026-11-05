# Settings history — approved inline plan, implementation record

The user approved moving History into Settings, file-click open, Windows Shell context menu and a red cross recycling action. Preserve month grouping/newest ordering, lazy bounded thumbnails, existing tray shortcuts and interrupted-session recovery.

- [x] Replace standalone CaptureHistoryWindow with disposable CaptureHistoryPage and activate/deactivate loading ownership.
- [x] Add Settings fifth section and route tray/history actions to it, including an existing Settings window. During recording only History remains available; editing preferences stays blocked.
- [x] Remove per-file Open/Folder and history-folder toolbar buttons. File button opens media, context request targets the same file, separate red cross recycles it.
- [x] Isolate Windows Shell menu/recycling behind injectable page callbacks and WindowsHistoryFileActions; rescan after menu/success, retain row on cancel/error, preserve errors independently from loading status.
- [x] Add English/Russian labels, headless page/navigation tests and recycle-only callback policy tests. Full suite405 passed, Release0 warnings/errors. Independent read-only reviews completed.
- [x] Portable package/source/archive consistency verified: ZIP integrity, three shipped DLL hashes, nested source/tracking files, current manual/version, no FFmpeg or portable settings; SHA256 sidecar generated.

Native menu is the classic Shell menu, not the Windows11 Explorer compact menu. Explorer in-place Rename is not advertised. Real Shell extensions, unavailable-bin behavior and gestures remain user acceptance; tests execute only managed fakes and unique temporary fixtures.

