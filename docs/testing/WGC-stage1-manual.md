# ShareVibeX 0.12.1 — native acceptance

These checks are user-owned. Automated tests do not capture the real desktop or initialize GPU/audio/clipboard devices.

1. Exit the old instance, extract the new portable folder, and copy only the desired settings into its `data` folder. Keep previous folders and media. Startup must remain tray-only.
2. In Capture settings select GDI, save, and verify quick Enter/Ctrl+C and screenshot editing, file export and clipboard equality. Repeat with strict Windows Graphics Capture, then Automatic. Screenshots must work without FFmpeg.
3. Selection must remain live/undimmed until screenshot editor entry. Verify moving desktop content produces fresh pixels at entry/copy, and editor pixels remain frozen afterwards.
4. Check normal SDR desktop, negative monitor origins, mixed DPI, portrait monitors and gaps between monitors. Geometry must match physical pixels; gaps should be black. Check cursor and capture toolbar exclusion.
5. Cancel acquisition, retry, disconnect/reconfigure a display during acquisition, and repeat captures. No partial screenshot, stale editor or forced Settings window should appear. A changed layout may require a new selection.
6. With HDR/advanced color enabled, Automatic should capture through GDI and show its reason in Settings status; when launched with `--diagnostics`, also record it in `data/diagnostic.log`. Strict WGC should explain its SDR requirement. Unknown color metadata should behave similarly. No HDR tone-mapping claim is made.
7. Check video with/without system sound and microphone, pause, preview, trim and export. Video remains FFmpeg/GDI; the screenshot method must not change its command or audio behavior.
8. Confirm both English/Russian settings, method diagnostics, history and transparent capture controls. Report the selected method, display layout/color mode and exact error for failures.

WGC can show the Windows capture indicator/border depending on the OS. Frame synchronization is bounded to100ms skew, not simultaneous capture. Native driver initialization/cleanup cannot be forcibly interrupted safely by the cooperative acquisition deadline.
