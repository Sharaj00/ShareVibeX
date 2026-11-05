# Linux port plan

## Scope

The first Linux target is x86-64 Wayland with a working session bus, XDG Desktop Portal backend, and PipeWire. Select the first distribution and desktop once a real test session is available. Verify a second desktop before advertising broad Wayland compatibility. X11 is a separate milestone.

Reuse the capture editor, recording state machine, FFmpeg encoding, preview/edit timeline, settings, and history. Keep Windows adapters and behavior covered by the Windows test suite.

## 1. Build and platform boundaries

- Give shared Avalonia code a platform-neutral target. Keep WinRT, Win32, WASAPI, and Windows shell integration behind the Windows host.
- Move the remaining platform calls out of capture selection, preview clipboard publishing, notifications, and settings. Reuse existing contracts for capture, global shortcuts, recording inputs, and preview audio.
- Add a Linux composition root with runtime capability checks. If tray integration is unavailable, provide a visible launcher with Capture, Settings, and Exit.
- Keep portable JSON next to the executable. Verify writable and read-only locations, Unicode paths, case-sensitive filesystems, and Linux Pictures-directory discovery.

**Acceptance.** Windows still builds and passes its suite. The Linux host starts in a real Wayland session and provides a usable entry point.

## 2. Portal capture and screenshots

- Implement a D-Bus request lifecycle with unique request tokens, response subscriptions before calls, explicit cancellation, and owned session cleanup.
- Prototype ScreenCast authorization through CreateSession, SelectSources, Start, and OpenPipeWireRemote. Consume the scoped PipeWire descriptor and selected node in a persistent capture adapter. Preserve timestamps and close every descriptor and stream on failure or exit.
- Establish a verified mapping between source pixels and Avalonia surface coordinates. Test scale changes, fractional scaling, monitor removal, and compositor-provided geometry before enabling multi-monitor capture.
- Route selected pixels through the shared region editor and PNG/JPEG/WebP exporter. Use the Screenshot portal as a possible separate still-image path when its backend exposes a suitable picker.
- Check how the target compositor positions overlays and whether they appear in the captured stream. Where an overlay cannot be excluded or placed safely, use a separate preview/editor surface.

**Acceptance.** A user can approve or cancel sharing, select and edit a real image, save it, and repeat capture without leaking a portal session.

## 3. Recording, audio, and preview

- Feed only the authorized region to FFmpeg with bounded buffering and backpressure. Keep crop geometry separate from frame timing. Verify whether continuous region movement can update the crop without restarting the portal stream or encoder.
- Map system playback and microphone to PipeWire or the session's compatible audio service. Verify each source independently, simultaneous capture, unplug/reconnect, and stop cleanup.
- Reuse the existing segment assembly, preview and A–B editing. Add Linux preview audio output and define pause behavior when the portal session remains open.
- Probe actual encoders, muxers, and decoders in the installed FFmpeg. Check software encoding first, then hardware options on supported hardware.

**Acceptance.** Record silently and with both audio sources. Verify pause/resume, early Stop/Cancel, device loss, export, and audio synchronization in a real session.

## 4. Desktop integration

- Register the capture action through the Global Shortcuts portal where supported. Keep tray or launcher capture available if shortcut binding is denied or unavailable.
- Verify image and file clipboard formats with real receiver applications. Keep the exported file outside the recording workspace. Test paste after the editor closes and after app exit with the target desktop's clipboard manager.
- Integrate file opening, history trash, theme following, and completion notifications using supported desktop interfaces. Treat window hover, cursor confinement, and fullscreen suppression as optional compositor-dependent capabilities.

**Acceptance.** Shortcuts, clipboard ownership, history actions, and quit behavior work on each named supported desktop.

## 5. Package and release

- Build a self-contained linux-x64 tar archive with executable permissions, a launcher, licenses, and a dependency list. Package FFmpeg separately only if a later distribution decision calls for it.
- Add build and fixture tests on Linux. Keep permission dialogs, capture, sound, tray, and clipboard in a real desktop acceptance checklist.
- Verify unpacking in a clean user account, folder relocation, missing portal and FFmpeg, no leftover subprocesses or sessions, and Windows regression tests. Publish the Linux artifact separately once these checks pass.

## References

- [ScreenCast portal](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html)
- [Screenshot portal](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Screenshot.html)
- [Global Shortcuts portal](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.GlobalShortcuts.html)
