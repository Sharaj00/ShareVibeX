# Stage4 batch1 — completion feedback

Date:2026-11-03. User requested moving timing/advanced controls to Preview, then continuing the roadmap. Scope is an independently runnable first Stage4 batch.

Implemented: Preview owns timing and advanced command controls. Portable CompletionOptions + bilingual General settings enable system completion sound and custom in-app notices (off by default), duration/fade, primary monitor corner/geometry and fullscreen suppression. Atomic export success emits exactly one feedback event; screenshots defer until overlay disposal, video only after Copy/Save. Selection/recording Block closes active notice and invalidates queued callbacks; errors/cancel/Stop do not emit success.

Runtime service isolates native fullscreen/sound/window callbacks for tests. Windows toast requires capture exclusion before Show; failure omits it. No activation, bounded expiry/fade, file/folder/close icons. Default system sound avoids bundled asset licensing and capture endpoint changes. Failures never fail an export.

Validation:343 internal tests (102 core +241 app), including defaults/persistence/invalid timing, Preview ownership, service gating/queue cancellation/fullscreen/faults, negative-monitor/DPI placement/fade and export-success vs canceled Save. Native user acceptance is separate.

Next batches: custom sound files and event types; independent mouse actions and bounded copied-image ownership; monitor selection. Do not silently retain screenshots or add downloads/autostart changes.
