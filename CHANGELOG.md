# Changelog

## 0.18.0 — 2026-11-05

- Add live region movement for structured WGC recording on one SDR monitor. Keep the encoder and audio streams open during a move.

- Show the region move grip from video setup through recording and finalization.
- Place application libraries in libs and save settings.json beside the launcher. Read legacy settings from data when the root file is absent.

- Combine image and video settings in the Capture / Media tab.
- Use a consistent row height throughout General.

- Adopt the supplied 0.17.5 settings as the fresh-install and reset defaults.
- Fit General in two columns and compact the Capture pages. Keep page scrolling in History.

- Prepare FFmpeg during the countdown, show the remaining seconds on the recording control, and allow a per-recording wheel override. Set the default in Capture / Media.
- Use tooltips in Selection and Overlays. Explain Auto for Preset and Tune and group audio devices under Devices.
- Choose the frame color from a palette and sample screenshot colors with an eyedropper.

- Center the mode picker and video setup using their arranged size.
- Use a narrower fixed-size settings window with rounded corners.
- Add live capture outline preview and thickness settings.

- Show the current Windows administrator status in General, with guidance for capturing elevated applications.
- Center capture toolbars beneath the selected region where space permits.
- Describe Preset and Tune based on the selected encoder.

- Apply settings automatically and preserve changes across tabs and when closing the settings window.
- Adjust recording FPS with the mouse wheel and toggle cursor visibility before recording starts.
- Prioritize the capture hotkey on the UI thread and log requests diverted or rejected before selection starts.
- Show the region selector without initial activation when the foreground window covers a monitor.
- Copy the foreground monitor directly when Windows reports exclusive Direct3D fullscreen, avoiding the focus change and display-mode reset caused by an interactive overlay.
- Log receipt of the Windows global shortcut when diagnostics are enabled.
- Keep the current and immediately previous portable builds. Preserve portable data and tools separately before pruning older builds.

## 0.17.1 — 2026-11-05

- First public portable Windows x64 release.
- Added top-level settings tabs, an interactive shortcut field, and expandable notification settings.
- Included the English README and the Wayland-first Linux port plan.
