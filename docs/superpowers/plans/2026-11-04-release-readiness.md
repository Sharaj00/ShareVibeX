# Stage 6 — release readiness, 0.10.2

Continuation of the approved consistency/portable-release roadmap. Native desktop/device acceptance remains assigned to the user. Deferred notification features, prepared recording and moving a recording region stay out of scope.

- [x] Inspect portable tool/settings boundaries and capture-action accessibility without launching native capture.
- [x] Reproduce legacy theme migration failing under Russian UI; keep schema keys invariant. Four regression cases: two failed against old code, all passed after the two-key correction.
- [x] Exercise a real temporary-folder move with Unicode/spaces, persisted preferences, relative FFmpeg discovery and absent optional tool; never execute the fixture tool.
- [x] Check vector icons, hints, automation names and keyboard focus across screenshot/mode/recording panels in English/Russian headless UI.
- [x] Remove obsolete future text-recognition dependency notice; record existing Linux seams and unsupported behavior in docs/linux-portability-assessment.md.
- [x] Final full Release build and internal test suite:0 warnings/errors;112 Core+281 App=393 tests passed.
- [x] Portable package/source/archive consistency verified: ZIP integrity, three shipped DLL hashes, nested source/tracking files, current manual/version, no FFmpeg or portable settings; SHA256 sidecar generated.

Scope of the language fix is legacy schema1 migration; it does not establish the root cause of the unreproduced native Save report SVX-008. UI/recording acceptance, Linux implementation and long-run native performance are not completed by these checks.
