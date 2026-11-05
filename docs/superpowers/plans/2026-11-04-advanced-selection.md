# Advanced selection — 2026-11-04

User skipped remaining notification enhancements and authorized advanced selection.

Implemented: portable SelectionOptions + bilingual Capture settings (Selection/Overlays/Snap sizes); optional external window/child-HWND bounds detection with own overlays excluded; live click-to-select vs manual drag; frozen-editor dim; size/position template tokens; independent screen/center crosshairs; Alt physical-size snap with Shift priority; active-entry-monitor region/cursor confinement.

Constraints: defaults preserve live undimmed selection and quick Enter/Ctrl+C. No RGB sampling, UI text scraping or background screenshots. Snap sizes are edited as a bounded semicolon/newline list, not named arbitrary expressions. Cursor scope saves/restores previous bounds, avoids replacing newer external constraints, unwinds on capture exits and has explicit app-exit cleanup. Native adapter methods are never invoked by the isolated tests.

Root reviewed/integrated runtime and native adapters from independent agents.380 tests pass (108 core +272 app),0 warnings/errors. Synthetic geometry/template/preset tests, headless hover/click/drag and settings roundtrip cover internal semantics. Native multi-monitor/DPI/window/control/confinement acceptance remains user-owned.

Remaining notification features (extra event sounds, full-result Copy/Edit and monitor choice) are deferred at user request, not completed. Future capture refinement can add a richer preset editor after native acceptance; current text list already supports adding/removing dimensions.
