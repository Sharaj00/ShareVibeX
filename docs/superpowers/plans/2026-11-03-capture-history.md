# Capture history implementation — 2026-11-03

User-confirmed scope: archive screenshots and edited videos after Copy/Save, not at Stop. Use a configurable default C:\Pictures\Screenshots root, capture-date yyyy-MM folders, and application_yyyyMMdd_HHmmss.ext names. Remove text recognition/translation from the product scope.

Implemented steps:
1. Add portable General history toggle and root directory; keep existing settings compatible. Default toggle is off to make archiving explicit.
2. Resolve the topmost external process at region center before screenshot freeze/quick copy or recording. Preserve capture time through editing.
3. Archive completed screenshot/video exports via temporary files and non-overwriting moves. Sanitize names and suffix same-second collisions.
4. Replace the tray retained-recordings entry with Capture history; retain interrupted-session recovery from that window.
5. Remove unused settings section and old text-processing plans. Create concise CODEX.md, BUGS.md and Russian TODO.md.
6. Validate isolated filesystem/settings/headless UI behavior; build portable release. Native acceptance remains with the user.

No background capture, new provider dependencies, cloud storage or automatic deletion was introduced. History archive errors are surfaced while already exported results remain intact.
