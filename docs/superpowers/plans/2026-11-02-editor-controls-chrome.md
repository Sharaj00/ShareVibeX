# Window chrome and direct editor controls

**Request:** User supplied a comparison showing System mode's black background/native caption and requested custom chrome, popup color palette, on-canvas resizable text, keyboard-only delete/history, wheel-adjustable thickness/blur during drawing, and Ctrl-held annotation movement.

**Execution:** Continue approved inline workflow. User owns real desktop/clipboard tests: no application/UI/native harness launches. Use internal/headless tests and a final independent source review, then package 0.2.2 with VERIFY.md.

1. Reproduce System dark palette with actual Fluent resources, not the bare headless app. Use explicit Windows 11 light/dark surface tokens plus Windows accent; Contrast remains true black. Add own draggable settings title bar, caption buttons and edge resize; remove native decorations. Style navigation with subtle selection and accent marker.
2. Add matching Avalonia ColorPicker package (MIT, same pinned version) and popup palette. Remove Select, Delete, undo/redo, text and font-size inputs from toolbar. Replace width spinners with focusable wheel-adjustable number. Keyboard undo/redo: Ctrl+Z / Ctrl+Shift+Z; Delete stays keyboard-only.
3. Latch Ctrl+pointer-down as an annotation-move gesture without switching the chosen drawing tool or changing gesture mid-drag. Wheel over the width number adjusts tool state; wheel during an active drawable gesture updates its draft and saved thickness/blur. Preserve rendering/export ordering and avoid no-op move history.
4. Text tool creates a blank draggable rectangle in the overlay. Embed a multiline text input with a resize grip; resizing fits rendered font to bounds, with no fixed text/size toolbar fields. Ctrl+Enter or clicking outside commits, Escape cancels the text edit first, Copy/Save commits before export. Existing text can be reopened by clicking it with Text. Use shared fitted text layout for export.
5. Verify regression suite and build only; review/correct important findings, update docs/licenses/source and portable 0.2.2. Manual Windows chrome, palette, wheel and text interaction checks go to the user.
