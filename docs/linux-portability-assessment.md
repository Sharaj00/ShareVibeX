# Linux portability assessment

Source inventory as of 0.10.2. This is a boundary assessment for the existing roadmap, not a Linux implementation or runtime-support claim. Windows remains the only implemented capture platform. No new dependency or Linux package is introduced.

## Reusable parts and current boundaries

| Area | Existing reusable seam | Windows coupling / remaining work |
| --- | --- | --- |
| Capture and monitor geometry | Core `IDesktopCapture`, physical `PixelRect` / `MonitorInfo`, shared capture workflow | `CompositionRoot` currently refuses non-Windows capture and constructs `WindowsDesktopCapture`. A Linux provider must acquire frames and describe the same coordinates/scales consistently. |
| Region selection and overlays | Shared Avalonia selection state, snap rules and annotations | Window/control detection, cursor confinement and capture exclusion call Windows helpers directly. Equivalent behavior needs platform capabilities; unsupported options must be disabled explicitly. |
| Global shortcut | Core `IGlobalHotkey` | Composition creates `WindowsGlobalHotkey`. Registration, conflicts, permission denial and cleanup need a Linux implementation. |
| Video input | Core recording/session/process contracts, FFmpeg argument model, shared assembler/export | Desktop-input arguments target Windows gdigrab; passing a Linux FFmpeg executable alone cannot make recording work. Capture-source negotiation and input arguments need a backend. |
| Recording audio | Core `IRecordingAudioFactory` / `ILiveRecordingAudioFactory` and disposable audio sources | Concrete implementation uses Windows endpoint capture and native IPC. System/microphone IDs, permissions, pause, cancellation and runtime failure need a Linux source factory. |
| Video preview | Shared decoder contracts, FFmpeg playback pipeline and edits | `IPreviewAudioOutputFactory` has a Windows output implementation. Linux must provide paced output, failure reporting and drain/disposal behavior. |
| Clipboard | App `IImageClipboard`, shared image export and durable video cache | Current ownership and Windows image/file formats require a Linux clipboard publisher. Copied files must survive app closure; receiving-app behavior needs independent checks. |
| Tray and startup | Shared gesture routing/settings and tray-only product flow | Native Shell tray, Explorer restart, HKCU Run and startup transaction are Windows-specific. Linux desktop support varies; no invisible/unreachable background process should result if tray integration fails. |
| Appearance and feedback | Shared Avalonia settings, localization, image-only toast and policies | Theme palette, sound, exclusive-fullscreen detection and overlay exclusion use Windows services. A Linux implementation needs explicit capability/fallback policy. |
| Portable settings and history | Core JSON/atomic writes, shared history/archive/thumbnail code | Relative tool discovery is covered after a Windows folder move. Linux path case, permissions, filename rules, executable permissions and clipboard/cache lifetime remain unverified. |

## X11 and Wayland must be treated separately

Do not infer runtime support from Avalonia compilation. Capture, global shortcuts, screen coordinates, window discovery, cursor confinement and overlays have different access models across display servers and desktop environments. Investigate supported system interfaces and user-consent flows before selecting dependencies. Window/control-hover selection and capture exclusion may not have an equivalent capability; do not silently promise them or record the app's own controls.

No automatic privileged helper, desktop service, tool download or startup registration is part of the current portable product. Missing platform capabilities must produce an actionable message while preserving usable functions.

## Acceptance required before any Linux release

- Choose and state the supported display server, desktop environment, architecture and distribution/runtime baseline.
- Verify live selection → fresh quick copy, frozen editing, negative coordinates/scaling and multiple monitors on real sessions.
- Verify recording with no audio, system audio, microphone and both; pause/resume, early Stop/Cancel, device loss, preview/edit and file export.
- Verify permission cancellation, absent tools/services, tray navigation, app exit and cleanup. Clipboard must work in receiving applications after preview closure and app exit.
- Verify settings/history after moving the portable directory, read-only folder failures, executable permissions and path case. Preserve originals on failed writes.
- Audit native dependencies and licenses for the actual target package; a Windows self-contained ZIP is not a Linux distribution.

These checks and backend design are future work, after Windows acceptance. The current release only records the actual seams and blockers.
