# Stage 3 — General preferences

User authorized the next roadmap stage and explicitly approved English/Russian language selection. Deliver in separate runnable batches to keep scope reviewable.

1. Startup batch (0.6.1): add Open settings on startup (portable preference) and Run ShareVibeX when Windows starts (actual per-user registration). Read the registration when Settings opens; write only after Save. Do not register automatically on launch. Preserve another application's conflicting startup value. Roll back registration if portable settings fail to save. Tests use an in-memory registry boundary, never real autorun.
2. Tray batch: configurable single/double/middle actions with gesture disambiguation and native adapter tests. Keep the tray visible until alternate navigation exists.
3. Localization batch: resource-based English/Russian selection across settings, tray, capture, preview, history/recovery and errors. English default; do not ship a partly translated interface.

Startup files: core AppSettings preference; WindowsStartupRegistration platform adapter; StartupSettingsTransaction for persistence rollback; App/SettingsWindow integration. Acceptance: no writes while viewing/canceling Settings, explicit Save enables/disables the correct quoted executable, moved copies can repair registration, startup visibility survives portable round-trip. Native registry/login acceptance remains user-owned.
