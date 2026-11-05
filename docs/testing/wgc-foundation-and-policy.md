# WGC Stage1 — foundation and routing batch

Completed approved tasks: S1.1 and S2.1. Native screenshot acquisition, monitor composition and production integration (S3/S4) remain pending. The latest public portable release remains0.11.2; this batch is not a WGC-enabled release.

## Verified build boundary

- App, Platform.Windows, App.Tests and Windows.Smoke target net10.0-windows10.0.19041.0 with WindowsSdkPackageVersion10.0.19041.57. Core/Core.Tests remain net10.0; Core lock graphs contain no package dependencies.
- Vortice.Direct3D11/DXGI/DirectX3.8.3, Vortice.Mathematics2.1.0 and SharpGen.Runtime/COM2.4.2-beta are pinned by package metadata/lock files. The beta version is Vortice's declared transitive dependency, not an unrecorded upgrade.
- Exact package metadata and upstream licenses are under third-party-licenses; Windows SDK terms are retained as the RTF supplied by its package license URL. THIRD-PARTY-NOTICES.md records the new graph.
- Compile-only API library validates CreateFreeThreaded, cursor configuration, frame content/timestamp/surface access, D3D11 staging creation/copy/map/unmap and DXGI-device to WinRT-device marshaling. It has no entry point and was never executed. The signature probe is retained as ApiCheck.cs.txt under docs/testing/wgc; it is not a runnable capture example or a correct capture lifecycle implementation.
- A separate self-contained publish to .superpowers/wgc-foundation-publish includes CoreCLR/hostpolicy, SDK projection/WinRT runtime, Vortice and SharpGen assemblies. SDK projection hashes match the pinned targeting package. No FFmpeg or user settings were copied. This static closure check does not prove runtime behavior on a clean machine.

## Screenshot policy and settings

- CaptureBackend defaults Automatic for missing JSON fields; unknown values are rejected without replacing user settings.
- The draft Capture settings control saves Automatic/WGC/GDI in both UI languages and states that video still uses GDI. Its presence in source is not a claim of functional WGC before S4 integration.
- ScreenshotCaptureRouter uses explicit GDI or strict WGC. Automatic catches only CaptureSourceUnavailableException; generic IO/data/topology/cancellation failures do not trigger a second acquisition.
- Preference is read once per request. Invalid preference and pre/late cancellation fail before publication/reporting. Report callbacks carry only backend/reason metadata; observer errors cannot break a successful screenshot or cause fallback.
- CompositionRoot still constructs the existing GDI source. The router will be wired only after the native WGC source passes its planned tests.

## Validation evidence

- TestResults/wgc-foundation-build.log:415 baseline tests passed after Windows target/dependency changes.
- TestResults/wgc-api-compile.log:compile-only API library,0 warnings/errors.
- TestResults/wgc-foundation-publish.log and .superpowers/verify-wgc-foundation.py:self-contained closure and exact SDK hash checks passed.
- TestResults/wgc-routing-red.log:initial missing-API compilation failure; TestResults/wgc-ui-red.log:two missing-control failures before UI implementation.
- TestResults/wgc-policy-build.log:locked restore, Release0 warnings/errors,121 Core+322 App=443 tests passed,0 failed/skipped.
- New coverage:7 settings cases,19 router cases and2 English/Russian UI save cases. A subagent's initial Debug testhost connection attempt timed out; no success was claimed for that run. The root Release full-suite run above is the execution evidence.
- One independent read-only batch review found no blocking issue in S1/S2. Native WGC sessions, capability queries, monitor/GPU creation, GUI, clipboard/audio and Windows smoke were not executed.

Next: S3.1 request-owned native monitor sessions, followed by S3.2 composition and S4 integration. User TODO additions for encoder-specific settings and key-combination assignment remain intact for subsequent work.
