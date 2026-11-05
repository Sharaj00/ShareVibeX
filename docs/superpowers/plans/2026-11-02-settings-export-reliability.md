# Settings and export reliability plan

**Scope:** User-reported crash after capture/save, clipboard durability, image format/quality settings, Windows-style settings layout and System / Contrast (AMOLED) themes. User performs all real desktop/manual checks; do not launch the app, automate UI, run the native smoke harness, or modify the live clipboard.

**Evidence:** Windows .NET Runtime crash event at 2026-11-02 15:31:04 identifies `Cannot re-show a closed window` in WindowVisibilitySnapshot.Restore, escaping CaptureWorkflow's finally and async-void capture entry. Clipboard previously used delayed bitmap ownership without FlushAsync; same-process pre-close tests did not prove post-window-close persistence.

1. Reproduce hidden-window closure with headless regression tests; track Closed windows, skip closed owners, clear event subscriptions, and prevent cleanup failures escaping the capture entry. Explicit lifetime mode remains tray-only.
2. Add clipboard transport boundary for deterministic publication/flush ordering tests. Publish and flush before completing capture; keep retained image alive through flushing/failure and release safely.
3. Migrate portable settings schema 1 to 2. Map Light/Dark to System, Amoled to Contrast. Add PNG/JPEG/WebP default format and quality 1–100; preserve invalid/future files. Encode the format corresponding to the selected filename; PNG stays lossless.
5. Run build and internal/headless regression checks only, obtain focused independent code review, package 0.2.1 with a manual verification checklist for the user. Never claim real clipboard or save-dialog behavior from headless tests.

Existing user approval covers this concrete correction and feature request; inline implementation continues without an additional permission/design round.
