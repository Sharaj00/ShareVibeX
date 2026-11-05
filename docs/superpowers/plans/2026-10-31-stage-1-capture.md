# ShareVibeX Stage 1 Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a portable Windows 11 application with three themes and one region-capture workflow invoked from the main window, tray, or global shortcut, producing PNG files and clipboard images.


**Tech Stack:** C#, .NET 10 LTS, stable Avalonia with Fluent theme, xUnit, Windows Win32 capture and hotkey APIs. Pin exact stable SDK/package versions during Task 1 and commit lock files; do not use preview versions. Prefer manual composition and small view models over introducing an unnecessary dependency container.

**Spec:** `docs/superpowers/specs/2026-10-31-sharevibex-design.md` (approved, including Recording-tab amendment).

## Global Constraints

- The confirmed name is ShareVibeX. The user selected C# and Avalonia.
- Windows 11 is the primary platform; the architecture must allow a later Linux build.
- Code, comments, and UI strings are English unless requested otherwise.
- Screenshot is always the initial mode.
- Store settings and operational data under an application-local data directory.
- No installer, service, or mandatory administrator privileges.
- Application license: GPL-3.0-or-later, approved with the design. No remote publication is authorized.

## Review Focus

- Mixed-DPI displays and negative monitor coordinates: selected output must match physical desktop pixels (Tasks 2 and 4).
- Rapid repeated shortcuts and cancellation: only one session may own overlays and bitmaps; cancellation returns to idle (Tasks 2 and 4).
- Read-only or corrupt portable settings: preserve existing data and explain recovery without silent profile-directory fallback (Task 1).
- Unicode/spaced destinations, failed writes, and clipboard failure: retain the current capture and permit retry (Task 5).
- Display reconfiguration during selection and application windows in the snapshot: cancel stale geometry safely and hide app surfaces before snapshotting (Tasks 3 and 4).

---

## File map and shared contracts

- `ShareVibeX.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`: build setup, exact package pins, nullable references, package lock generation.
- `src/ShareVibeX.Core/Settings/{AppSettings,PortableSettingsStore}.cs`: versioned JSON, theme, shortcut, output directory, optional data path.
- `src/ShareVibeX.Core/Capture/{PixelPoint,PixelRect,MonitorInfo,CapturedFrame,DesktopSnapshot,CaptureSession,CaptureCoordinator}.cs`: physical geometry, owned pixel buffers, selection lifecycle.
- `src/ShareVibeX.Core/Platform/{IDesktopCapture,IGlobalHotkey,ISettingsStore}.cs`: platform-neutral contracts.
- `src/ShareVibeX.Platform.Windows/{WindowsDesktopCapture,WindowsMonitorProvider,WindowsGlobalHotkey,NativeMethods}.cs`: isolated native implementation, `net10.0-windows`.
- `src/ShareVibeX.App/{Program,App}.cs`, `App.axaml`, `CompositionRoot.cs`: cross-platform entry point, runtime Windows adapter loading, application lifetime and per-monitor DPI initialization before windows.
- `src/ShareVibeX.App/Views/{MainWindow,SettingsWindow,SelectionOverlayWindow,CaptureResultWindow}.axaml` and matching `.axaml.cs`: small focused views.
- `src/ShareVibeX.App/Capture/{SelectionOverlayController,CaptureWorkflow,BitmapExportService}.cs`: window lifetime, entry point, Avalonia bitmap/clipboard/file integration.
- `src/ShareVibeX.App/ViewModels/{MainViewModel,SettingsViewModel,CaptureResultViewModel}.cs`: observable state and actions.
- `src/ShareVibeX.App/Themes/{Light,Dark,Amoled}.axaml`: application tokens, contrast and focus visuals.
- `tests/ShareVibeX.Core.Tests/`: state, settings and geometry tests; `tests/ShareVibeX.App.Tests/`: export/workflow tests using fake platform dependencies, headless Avalonia only if needed.
- `scripts/{build,publish}.ps1`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `docs/testing/stage-1-windows.md`: reproducible local build, ZIP, and evidence.

`PixelRect(int X, int Y, int Width, int Height)` uses physical pixels and right/bottom-exclusive bounds. `MonitorInfo(string Id, PixelRect Bounds, double Scale)` represents monitor geometry. `CapturedFrame(int Width, int Height, int Stride, byte[] Bgra32)` contains top-down BGRA pixels with explicit alpha set opaque for desktop capture. `DesktopSnapshot(PixelRect Bounds, IReadOnlyList<MonitorInfo> Monitors, CapturedFrame Frame)` spans the virtual desktop; gaps are opaque black. These contracts contain no Avalonia or Windows types.

## Task 1: Buildable shell, portable settings, themes

**Files:** Build setup; Core settings and `ISettingsStore`; App entry/composition, Main/Settings views and view models, theme files; `tests/ShareVibeX.Core.Tests/PortableSettingsTests.cs`.

**Interfaces:** Produce `AppSettings(int SchemaVersion, ThemeMode Theme, string CaptureHotkey, string? OutputDirectory)` with version 1, Dark, Ctrl+PrintScreen, and null defaults. Produce `ISettingsStore.LoadAsync(CancellationToken): Task<AppSettings>` and `SaveAsync(AppSettings, CancellationToken): Task`. `PortableSettingsStore(string dataDirectory)` implements this contract and uses `settings.json`. `ThemeMode` values: Light, Dark, Amoled. Surface load/write errors to presentation; no hidden fallback.

- [ ] Locate an existing .NET SDK; if unavailable, obtain an official .NET 10 SDK into ignored workspace-local `.tools/dotnet`, without changing global PATH. Resolve stable Avalonia/xUnit dependencies from official package metadata, pin exact versions, create solution/projects and package locks. Record build invocation in README. Keep App `net10.0` and Windows adapter separately targeted; load the adapter only on Windows so future Linux builds do not need Windows APIs at startup.
- [ ] Add failing settings tests: missing file gives the exact defaults; saved Amoled and a Unicode output path round-trip; malformed JSON leaves original bytes intact and raises a recoverable error; unsupported future schema is not overwritten; an injected write failure leaves the previous valid file untouched. Run `dotnet test --filter PortableSettingsTests` and confirm assertions fail before implementing behavior.
- [ ] Implement settings with temporary-file/atomic replacement, a writable-directory check, and explicit UI recovery options. Resolve normal data path as `AppContext.BaseDirectory/data`; support explicit `--data-dir <path>` to choose a writable portable location. Recovery requires the user's Reset or directory choice, never silent deletion or AppData fallback.
- [ ] Build the shell and Settings appearance/capture controls. Apply three themes immediately and persist the selected theme. Add accessible labels, visible keyboard focus, tray Exit/Show, close-to-tray behavior, and ensure Exit terminates the process. Unsupported-platform startup displays a clear capture-unavailable message while keeping shared UI buildable.
- [ ] Run `dotnet test --filter PortableSettingsTests` (all pass) and `dotnet build ShareVibeX.slnx -c Debug` (zero errors). Manually check all themes, restart persistence, close-to-tray and Exit. Record actual results; commit if a local Git repository exists, without creating a remote.

## Task 2: Physical geometry and one-session state machine

**Files:** Core capture contracts/coordinator; `tests/ShareVibeX.Core.Tests/{PixelRectTests,CaptureCoordinatorTests}.cs`.

**Interfaces:** Produce `PixelRect.FromPoints(PixelPoint a, PixelPoint b): PixelRect`, `Intersect(PixelRect other): PixelRect`, and `ToFrameRect(PixelRect desktopBounds): PixelRect`. Produce `CaptureState { Idle, Selecting, Ready }`; `CaptureCoordinator.TryBeginSelection(): bool`, `SetSnapshot(DesktopSnapshot snapshot): void`, `CompleteSelection(PixelRect region): void`, `Cancel(): void`, `EndSession(): void`, and read-only `State`/`Session`. `CaptureSession` holds the desktop snapshot, selected desktop rect and `CaptureMode.Screenshot` without UI objects. Invalid transitions fail predictably, never create half-initialized sessions.

- [ ] Add failing geometry tests: dragging from (200,100) to (-100,-50) yields (-100,-50,300,150); region (-1920,0,101,51) relative to desktop (-1920,0,3840,1080) yields (0,0,101,51); intersections clip right/bottom exactly; zero-area selection is rejected. Run `dotnet test --filter PixelRectTests` and confirm failure.
- [ ] Implement integer physical geometry. Treat per-monitor UI units only in presentation; do not apply a global desktop DPI multiplier.
- [ ] Add failing coordinator tests: a second begin during Selecting or Ready returns false; completion requires a snapshot and nonempty in-bounds region; Cancel from Selecting returns Idle with no frame retained; EndSession from Ready releases the session; new sessions default to Screenshot. Run `dotnet test --filter CaptureCoordinatorTests` and confirm failure.
- [ ] Implement state transitions and ownership; snapshot buffers become unreachable when the session ends and no stale UI callback can complete a cancelled session. Use a monotonically increasing request token in workflow to reject stale asynchronous completions.
- [ ] Run both test classes (all pass). Review contracts against Task 4 consumers and commit the coherent geometry/state deliverable if Git is available.

## Task 3: Windows desktop capture and global shortcut

**Files:** `IDesktopCapture`, `IGlobalHotkey`, Windows adapter/native methods; App composition and shortcut settings integration; `docs/testing/stage-1-windows.md`.

**Interfaces:** `IDesktopCapture.GetMonitors(): IReadOnlyList<MonitorInfo>` and `CaptureAsync(CancellationToken): Task<DesktopSnapshot>`; `IGlobalHotkey.Register(string gesture, Action onTriggered): void`, `Unregister(): void`, `Dispose(): void`. Registration errors are surfaced without pretending success. The adapter owns native handles and registration lifetime.

- [ ] Implement monitor enumeration in physical coordinates with monitor-specific scale, per-monitor-v2 DPI awareness initialized before UI/native windows, and a consistent virtual desktop union. Document that stage-1 still capture uses GDI screen copy; HDR fidelity is subject to real-machine verification.
- [ ] Implement desktop snapshot acquisition using Win32 BitBlt with CAPTUREBLT into a DIB, top-down BGRA conversion and opaque alpha. Release every DC/bitmap in success and failure paths. Check cancellation and discard incomplete frames.
- [ ] Implement a message-only/native hotkey host with RegisterHotKey and no-repeat behavior. Marshal callbacks to Avalonia UI thread. Applying a new shortcut must preserve/recover the old registration if replacement fails; show conflict errors in Settings. Dispose registration on Exit.
- [ ] Verify on Windows: shortcut works while another app is focused; occupied shortcut produces an error; held shortcut does not repeat captures; PNG fixture from the backend matches a known visible colored region and contains opaque alpha. Exercise repeated captures and inspect native resource growth. Record actual results rather than adding tests that merely assert native method wrappers exist.
- [ ] Build the solution successfully and commit if Git is available. If real interactive capture cannot be checked, mark those checks unverified and retain a testable backend harness for Task 6.

## Task 4: Shared capture workflow and selection overlay

**Files:** Selection view/controller, CaptureWorkflow, MainViewModel commands, tray commands; `tests/ShareVibeX.App.Tests/CaptureWorkflowTests.cs`.

**Interfaces:** `CaptureWorkflow.StartAsync(CancellationToken): Task` is the single entry point used by button, tray and shortcut. `SelectionOverlayController.SelectAsync(DesktopSnapshot, CancellationToken): Task<PixelRect?>` returns physical desktop bounds or null; controller owns and closes all overlay windows. `CaptureWorkflow` consumes the Task 2 coordinator and Task 3 capture service, then opens a result view for a completed session.

- [ ] Add failing workflow tests with fakes: all entry points invoke the same workflow; overlapping requests cause one capture only; cancellation closes overlays and restores hidden windows; a delayed snapshot after cancellation cannot open a result; a changed monitor topology cancels the selection with an actionable message. Run `dotnet test --filter CaptureWorkflowTests` and confirm failure.
- [ ] Implement workflow ordering: reserve session, hide all app surfaces that could appear in capture, await the next UI/compositor presentation opportunity, acquire snapshot, then open overlays. On completion crop from the same snapshot, not a later screenshot. Restore appropriate app visibility after completion/cancel/error.
- [ ] Implement one overlay per monitor backed by the shared snapshot, rather than one globally scaled window. Convert pointer positions through each monitor's physical origin/scale, maintain one global drag rectangle across windows, render dimmed background and dimensions, and capture the pointer while dragging. Esc/right-click cancels; releasing a nonempty selection completes. Monitor gaps remain black, matching the shared frame.
- [ ] Add deterministic mapping assertions: monitor origin (-1920,0), scale 1.5, local pointer (100,40) maps to physical (-1770,60); crossing a 100%/150% boundary does not apply either scale twice. Verify fakes and mapping assertions pass, then manually check reverse drags and cross-monitor selection where hardware is available.
- [ ] Recheck the physical selection against current topology before completion. End all native/UI resources on cancellation, repeated shortcut, display removal and application exit. Run full tests and build; commit if available.

## Task 5: PNG export, clipboard, result controls

**Files:** BitmapExportService; CaptureResult view/view model; `tests/ShareVibeX.App.Tests/BitmapExportTests.cs`.


- [ ] Add failing export tests: a 2x2 crop from a colored fixture preserves exact pixel positions and alpha; negative desktop coordinates produce the correct crop; saved PNG decodes with matching dimensions/pixels; destination `test output/日本語 image.png` succeeds; failed/cancelled save preserves an existing destination and removes its own temporary file. Run `dotnet test --filter BitmapExportTests` and confirm failure.
- [ ] Implement crop and PNG encoding with Avalonia bitmap facilities; use explicit BGRA/alpha semantics. Save through a same-directory temporary file, replace only on success, and keep the capture ready after errors. Default suggested name: `ShareVibeX-yyyyMMdd-HHmmss.png`; ask before overwriting through Save As.
- [ ] Implement clipboard image copy with the platform-supported Avalonia clipboard API, preserving alpha/dimensions. Catch clipboard-unavailable errors, display retry guidance, and retain the capture. Hide app UI during Select Again; close the old session only once the user requests replacement.
- [ ] Run export tests and full build. Manually paste into a Windows image-capable application and inspect actual file output. Confirm cancelled Save As leaves the capture open and successful explicit copy/save does not unexpectedly create an archive. Commit if Git is available.

## Task 6: Portable stage-1 package and handoff

**Files:** build/publish scripts, README, LICENSE/notices, stage-1 testing record, stage roadmap.

**Interfaces:** `scripts/build.ps1` locates system or `.tools/dotnet` SDK and runs locked restore/build/test; `scripts/publish.ps1` produces `dist/ShareVibeX-stage1-win-x64.zip` with self-contained app, required native libraries, licenses and portable data directory, excluding SDK/source caches/private data. A source checkout remains independently buildable.

- [ ] Add build and publish scripts with literal path handling, workspace-scoped output paths, no global installation, and no shell-string construction from user settings. Publish using `dotnet publish src/ShareVibeX.App/ShareVibeX.App.csproj -c Release -r win-x64 --self-contained true`; explicitly include/load the Windows adapter and required Avalonia native dependencies. Do not enable trimming/AOT without separate verification.
- [ ] Include GPL-3.0-or-later license text and third-party notices for actual packaged dependencies. Document portable data, shortcut conflict handling, screenshot-only stage scope, build commands, and remaining stages. No borrowed ShareX source is necessary in stage 1.
- [ ] Run locked restore, Release build, full tests, and publish. Unpack to a distinct workspace folder containing spaces and launch that executable, proving the package does not rely on the checkout's data or optional tools. Verify no FFmpeg/Python/model dependency for screenshot capture.
- [ ] Complete the Windows acceptance record: three themes; all capture entry points; screenshot save/paste; cancel/retry; shortcut conflicts; no app surfaces in capture; read-only data-path recovery; negative coordinates/mixed DPI if equipment permits; tray Exit; note HDR, unavailable hardware, or other unverified cases explicitly. Measure idle memory/startup for a baseline without inventing a performance threshold.
- [ ] Self-review changed files against this plan and the approved design. Obtain an independent stage review using a cost-conscious subagent if execution method permits; resolve substantive findings and rerun only affected checks. Commit the verified deliverable when Git is available. Report artifact location, actual checks, limitations, and next stage; stop at the stage boundary.

## Later-stage roadmap and Recording amendment

Stage 2: annotation objects, masks, blur/fill/outline, pen/arrow/text, undo/redo and identical preview/export pixels.

Stage 3: first prove live capture/audio/FFmpeg pipeline, then recording and dedicated Settings > Recording. Include ShareX-inspired capability-dependent encoder controls, bitrate/quality/preset/tune, audio codec/settings, source/device selection, frame rate, cursor, scaling (percentage, target dimensions, divide by factor, aspect ratio, algorithm, resulting dimensions), additional arguments, full custom argument templates with placeholders, effective preview and reset. Test custom/generated modes separately, scaling/filter conflicts, hardware initialization failure, silent-mode audio exclusion, odd sizes and safe output finalization. Write a dedicated detailed plan before implementation.


## Self-review outcome

Scope is deliberately limited to approved stage 1. All five Review Focus conditions have explicit owning checks above. Session/geometry contracts are shared consistently across tasks. Recording-tab changes are captured in the approved design and stage-3 roadmap, not silently deferred out of project scope. No product code or SDK installation is part of this planning deliverable.

## Execution outcome

Stage-1 working software and portable ZIP delivered. Release build: zero warnings/errors; full suite: 41 passing tests. Actual Windows harness passed desktop pixel, PNG, clipboard and native hotkey checks at 150% scale on one monitor. Extracted self-contained ZIP launched from a separate path containing spaces; all three themes and portable persistence were checked. Independent review found two important window-lifecycle issues, both reproduced and fixed with regression tests, and one minor theme-preview reset retained for the next UI iteration.

