# Live selection and compact Video settings

> Use superpowers:executing-plans inline with one independent source review at the end.

**Goal:** Replace the long Video form with a compact ShareX-style layout, and select a region on the live undimmed desktop before deciding the capture mode.

**Architecture:** Keep existing recording option/probe/validation contracts. Reorganize Video as compact video/audio columns with secondary tabs for scaling and command preview; advanced arguments/template stay directly accessible. Replace the initial snapshot workflow with desktop geometry only. A capture-mode session acquires fresh pixels only for quick capture or explicit editor entry. Transparent monitor windows draw only selection/hints while choosing or in Video mode. Hide all overlays before fresh capture; reuse the frozen image only within screenshot editing.

**Spec:** Existing approved design plus user's 2026-11-03 correction and ShareX reference screenshot. This correction takes precedence over the old freeze-on-invocation behavior. User already authorized implementation; no repeated design approval.

## Scope

- Enter/Ctrl+C in mode selection copies a fresh unedited crop and closes only the capture session.
- Explicit Edit screenshot captures a fresh frame and enters the existing in-place editor. Only then show frozen pixels/dimming/editor tools.
- Video selection stays live; recording itself remains the next recording-runtime tranche. Its controls must clearly indicate this limitation.
- Preserve physical coordinates, multi-monitor geometry, cancellation, clipboard/save behavior and all recording settings.
- English code/comments/UI. No app launch/native capture/live clipboard or actual FFmpeg/device checks: user owns real desktop verification.
- Reuse current checkout; no Git metadata/worktree.

## Tasks

1. **Compact Video form:** Reuse controls/draft state in VideoSettingsPage; compact labels, two equal columns, primary encoding tab, Scaling tab and Preview tab. FFmpeg path/test and additional/custom commands remain available. Put detailed help in tooltips/expandable help. Headless measure at the actual settings-page width and verify no tall default form and state survives tab switching.
2. **Live capture contracts:** Add DesktopLayout (geometry only) and CaptureModeSession. Update ICapturePresentation/CaptureWorkflow to return a completed CaptureSession rather than taking a pre-captured snapshot. Test no capture on invocation/cancel/video, fresh quick captures/editor entry and cancellation/topology changes.
3. **Overlay integration:** Transparent overlay windows and live region-only rendering, chooser/video toolbars, fresh acquisition with overlays hidden and compositor wait, frozen editing only after explicit action. Tests for transparent render pixels and capture timing/hide/restore callbacks without native desktop.
4. **Review/delivery:** Full internal suite, independent source review, focused fixes, portable 0.3.1 and new manual checklist. Never claim native transparency/capture acceptance from headless checks.
