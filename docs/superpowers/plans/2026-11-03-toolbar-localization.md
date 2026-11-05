# Screenshot toolbar and localization — 2026-11-03

User requested a fixed centered screenshot toolbar, consistent icon sizes, removal of Button size/Startup window, then continuation of the approved English/Russian localization batch.

1. Replace font glyphs with 22px vector icons and fixed 40px buttons. Keep tool/effect/width slots stable. Compute 690px preferred width independent of capture size, clamp to monitor, scroll horizontally on narrow monitors; center under region.
2. Remove both General options; ignore old button-size/startup-window values at runtime and reset deprecated fields on next Save for backward JSON compatibility.
3. Add explicit English-default language preference, exact resource keys/format placeholders and embedded Russian catalog. Translate owned UI/status/errors while preserving technical identifiers and user data; language applies after Save and persists.
4. Validate regression tests, resource placeholders, persistence and representative Russian headless UI. Build portable 0.7.0 with a user checklist; do not run native GUI/devices/clipboard acceptance.
