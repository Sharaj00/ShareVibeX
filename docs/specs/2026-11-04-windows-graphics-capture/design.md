# Windows Graphics Capture — approved design

## Goal and approval

The user approved WGC for screenshots and video, delivered in stages, retaining GDI as fallback. The approved architecture uses native WGC for screenshots and FFmpeg gfxcapture for video. Selection remains the existing live, undimmed region workflow. This document records that design; it is not an implemented-feature claim or an executable task plan.

## Current boundaries

- App/Core target net10.0. WindowsDesktopCapture implements Core IDesktopCapture with BitBlt; CaptureAsync returns a complete DesktopSnapshot with physical desktop bounds and BGRA pixels.
- FfmpegCommandBuilder currently hardcodes gdigrab; FfmpegCapabilities.Missing requires it regardless of other source capabilities. Audio is a separate input, with existing segmented pause/resume and immutable edit/export behavior.
- A current screenshot requires no FFmpeg. Preserve this property. FFmpeg discovery remains system-first/explicit-path/application-local; no bundled/downloaded executable.
- Native capture/device/GUI acceptance remains assigned to the user. Internal tests use fakes, synthetic files and headless UI. No native capture experiment is authorized by this design approval alone.

## User-visible behavior

One capture-method policy offers Automatic, Windows Graphics Capture and GDI, localized in English/Russian. Automatic prefers WGC when the selected operation can start successfully; GDI remains explicitly selectable. Missing preference fields use Automatic; retain the existing settings schema compatibility and all unrelated values.

Automatic may fall back to GDI only during acquisition/startup, before a result or recording is committed. Expose the effective method and reason in existing status/help surfaces. Explicit WGC reports an actionable failure rather than silently selecting GDI. Do not classify encoder/audio/output errors as WGC-source failures or retry them through GDI.

Once video has started, preserve the chosen backend for that recording session, including resumed segments. Runtime source failure follows the current safe stop/finalization behavior; no invisible backend switch or joined timelines from different capture methods.

Keep Enter/Ctrl+C fresh quick capture, screenshot-editor freeze timing, region resizing, annotations, clipboard/history, capture exclusion and current icon panels. Do not add a second source-selection window or a Windows picker to the normal region workflow.

## Stage 1 — native screenshot capture

Add an isolated Windows WGC implementation behind IDesktopCapture and a small selection/fallback boundary with the existing GDI implementation. Core remains independent of WinRT/Direct3D. The native implementation owns its D3D resources, frame-pool callbacks, capture sessions and cancellation; no always-running desktop capture starts with the tray app.

Acquire fresh monitor frames only when the existing workflow requests a screenshot. Assemble the virtual desktop using physical monitor coordinates, including negative origins and gaps, so existing crop/editor code consumes the same DesktopSnapshot contract. Preserve screenshot cursor behavior and the current opaque BGRA/SDR export contract. Full HDR output is not added by changing the capture source.

Bound frame acquisition and cleanup; do not wait indefinitely for a frame. Cancellation, display change, initialization failure and device loss must close sessions and release callbacks/resources. Multi-monitor capture must produce a complete consistent result or fail/fall back as a whole, rather than publish a partial desktop.

Implementation planning must verify the WinRT/D3D binding and build-target approach against the pinned net10.0 solution before selecting dependencies. Keep Windows integration isolated so Linux preparation is not coupled to Windows APIs in Core. Record any added runtime dependency/license in the portable package.

## Stage 2 — video through FFmpeg gfxcapture

Resolve support from the selected FFmpeg's actual filter/device/help capabilities; a version string alone is insufficient. Update preflight requirements by resolved source, rather than requiring gdigrab for a WGC request.

Resolve the selected physical desktop region into monitor capture targets and monitor-relative crop coordinates. Do not assume FFmpeg monitor indices equal application monitor ordering. Build source/filter arguments with the current structured argument/validation conventions, preserving audio mapping, cursor preference, target dimensions and encoding settings.

gfxcapture returns D3D11 hardware frames. Account explicitly for hardware-to-software transfer/format negotiation where the current scaler, composition or encoder requires software frames. The present CPU-oriented pipeline does not become zero-copy merely because its capture source changes.

Cross-monitor regions keep their existing meaning. Plan monitor composition and adapter compatibility explicitly; if WGC cannot start for the complete selected region, Automatic may choose GDI before recording begins. Explicit WGC must not silently capture only one monitor. Preserve negative coordinates, gap handling, fixed output geometry and the existing display-change stop policy.

Retain custom FFmpeg templates as explicit user commands. Do not silently rewrite a gdigrab custom template as WGC; show the template's effective source separately from structured Automatic/WGC selection. Keep editing/export graphs independent of the capture template.

## Verification and delivery

- Stage1 portable build: fake-session lifecycle/fallback/freshness, multi-monitor geometry/composition, unchanged screenshot workflow and settings migration tests; user checks real WGC/GDI captures, exclusion, mixed DPI and color.
- Stage2 portable build: source-specific capability parsing, monitor/crop/filter graph, software/hardware encoder transitions, audio mapping, startup-only fallback and session backend locking tests; user checks actual frames, audio sync, repeated pause/resume, first-frame/early Stop/Cancel, crop/scaling and hardware failures.
- Preserve existing data and earlier portable releases. Publish source/licenses/current manual checks with each build. A successful build or fake test is not proof of native WGC performance or compatibility.
- Existing TODO edits remain authoritative. Do not restore removed deferred features or combine this work with Linux implementation, recording preparation, moving capture regions, OCR or notification expansion.

## Primary references

- [Microsoft screen capture lifecycle](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture).
- [Desktop CreateForMonitor interop](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createformonitor).
- [FFmpeg gfxcapture source](https://raw.githubusercontent.com/FFmpeg/FFmpeg/master/libavfilter/vsrc_gfxcapture.c): monitor/HMONITOR targeting, crop/cursor/frame-rate options and D3D11 output. This upstream reference informs design; runtime options must still be verified in the user's executable.

Next artifact: an implementation plan for Stage1 based on local build/dependency feasibility. No implementation changes are included in this design-only step.
