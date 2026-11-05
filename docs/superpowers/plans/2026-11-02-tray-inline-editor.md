# Tray and in-place editor implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, with focused tests and one independent final review.

**Goal:** Apply the user's explicit correction: start only in the tray and select/edit the screenshot on the original desktop location, with no main or result window.
**Architecture:** Preserve the common snapshot/coordinator pipeline. Keep selection overlays alive during annotation and export; use a shared document and renderer for every monitor and saved pixels. Native screen capture stays unchanged.
**Spec:** Approved 2026-10-31 design and user amendment dated 2026-11-02. This implements the existing editor scope plus the tray-only correction; no new architecture choice or provider behavior is introduced.

## ShareX investigation

Read upstream RegionCaptureWindow.axaml.cs, RegionCaptureToolbar.cs, RegionSelectionOverlay.cs, RegionCaptureOptions.cs, and official region-capture documentation. ShareX defaults QuickCapture=true; disabling it keeps the rectangle with movement/resize nodes and finishes on Enter/double-click. Its annotation workspace and toolbar are embedded in the capture host, with tool options and region/annotation switching. Reproduce that interaction for the requested subset, not all ShareX features or its separate after-capture editor.

## Tasks

1. Add annotation document/history and shared raster composition. Test editable objects, movement, undo/redo, branch history, actual fill/ellipse/blur/export pixels.
2. Replace the selector's immediate completion with selecting/editing states in the same monitor overlays. Add a compact toolbar, region move/resize, selection/move/delete, rectangle/ellipse outline/fill/blur, pen/line/arrow/text, color/width/text size, undo/redo, copy/save, reselection and cancel. Render shared desktop composition without capturing toolbar/dim overlay into exports.
3. Remove main/result window use. Initialize settings once at startup; tray menu exposes Capture region, Settings, Exit. Show errors in settings or the active overlay. Keep optional settings hidden/restored during capture. Exit cancels any active capture. Eliminate the previous unsaved theme-preview reset.
4. Verify the full suite, native desktop/PNG/clipboard harness and real UI where possible. Request one cost-conscious independent review, fix important findings with regression tests, then package version 0.2.0 with source/licenses and document remaining hardware limits.

## Review focus

- Capture stays open after selection and no separate result window is created.
- Preview and clipboard/file output use identical annotation pixels; toolbar and dimming are excluded.
- Blurred ellipse does not affect corner pixels outside its mask; fill is opaque when selected.
- Pointer capture, resizing, negative desktop origins and cross-monitor coordinates remain physical pixels.
- Startup has no main window; settings recovery, shortcut conflict reporting, cancelling exports and shutdown remain reachable through the tray.

## Execution

Execution method remains inline as previously approved. User explicitly requested these behavior changes; proceed without another design-approval round. Track evidence in `.superpowers/sdd/2026-11-02-tray-inline-editor/progress.md`.
