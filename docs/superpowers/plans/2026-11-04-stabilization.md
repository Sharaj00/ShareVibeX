# Stabilization 0.10.1

Scope approved by the user: close verified issues, defer disproportionate/noncritical work; retain user-owned native acceptance.

- [x] Audit recording shutdown, history/preview ownership and cursor scope exits.
- [x] Fix Exit waiting for unfinished preview setup before cancellation. Regression timed out against old code and passed after correction; recovery media remains intact.
- [x] Isolate inaccessible/disappearing history files/months; cancellation continues to propagate. Three deterministic regression cases.
- [x] Verify cursor restriction scope disposal on setup failure and cancelled selection using injected callbacks/headless windows only.
- [x] Release pending raw toast preview reference after one-shot load; pointer actions retain metadata only. Static closure-ownership review; no GC timing claim.
- [x] Separate verified defects from pending native acceptance in BUGS.md; defer prepared recording architecture.
- [x] Final Release build and full internal tests:0 warnings/errors,386 passed. See docs/testing/2026-11-04-internal-0.10.1.md.
- [x] Portable package/source/archive consistency verified: ZIP integrity, three shipped DLL hashes, nested source/tracking files, current manual/version, no FFmpeg or portable settings; SHA256 sidecar generated.

No GUI, real desktop/audio/clipboard/cursor or startup acceptance is authorized in this maintenance pass. Prepared capture needs timestamp/input coordination; simple OS process suspension is documented as insufficient, not as proof that all preparation designs are impossible.
