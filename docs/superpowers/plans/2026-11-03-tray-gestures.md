# Tray gesture batch — 2026-11-03

Stage 3 batch 2, authorized by continuation of the existing General plan. Default gestures: single left Capture region, double left Open settings, middle None. Every gesture can select None/Capture/Settings/Stop/History; right click remains the reachable menu. No hide-tray option is added.

Avalonia 12.1.4 exposes only TrayIcon.Clicked. The Windows adapter therefore owns a Shell_NotifyIcon v4 icon and hidden top-level message window, native popup menu, Explorer TaskbarCreated restoration, tooltip and icon handle lifetime. If native construction fails, App retains the Avalonia fallback (single action only). Other platforms keep that fallback; double/middle configuration is a Windows feature.

TrayGestureRouter separates the double-click interval from UI/native state. First left-up waits for GetDoubleClickTime; double-click cancels pending single and suppresses the following up. Context/middle/keyboard activation cancels pending single. Keyboard activation invokes the single action immediately. App dispatches against current settings and checks recording state.

Validation: Release build and 315 internal tests pass (95 core + 220 app). New tests cover single delay, double suppression, cancellation, settings round-trip/invalid enum rejection, and ICO directory extraction/bounds. No native tray was launched. Native event delivery, Explorer restart, menu keyboard behavior and high-DPI icon presentation require user acceptance.

References: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw and https://learn.microsoft.com/en-us/windows/win32/shell/notification-area .
