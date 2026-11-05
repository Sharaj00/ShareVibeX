# Stage4 batch2 — custom sound and mouse actions

Date:2026-11-04. Continues the approved sounds/notifications roadmap.

Custom WAV path, browse and explicit preview are provided in General. Relative paths resolve against the portable executable folder; empty/unavailable/invalid custom media falls back to the system scheme. RIFF/WAVE metadata/bounds and16MiB cap are validated without decoding or playing in tests. Async PlaySoundW is owned by the app; Block/Dispose stops it before capture, including default system alias playback. No external audio dependency or sound assets are introduced.

Independent left/right/middle actions: None/Open file/Open folder/Close; defaults None/Close/None preserve existing behavior. Successful file actions close the toast. Missing result files leave it open and expose a localized hint. The image-only appearance is unchanged; no extra screenshot retention is introduced.

363 internal tests pass (103 core +260 app). New checks cover WAV resolution/validation, exact result-file dispatch, missing-file/Close handling, settings persistence, invalid action rejection and capture-block/exit sound stop callbacks. Native testing remains user-owned.

Next: separate sound event types, bounded full-result ownership for notification Copy/Edit and monitor selection. Do not use reduced preview pixels as the original screenshot for Copy/Edit.
