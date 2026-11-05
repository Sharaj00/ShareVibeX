# Windows review

ShareVibeX is a portable desktop capture application. The review assumes one interactive user, potentially running multiple portable copies that share a capture history. It considers both recording and exporting while the app holds temporary media.

| Priority | Finding | Change |
| --- | --- | --- |
| High | The publisher put arbitrary root files in the source archive. A local log in the checkout would ship. The hard-coded version default could also give a new build an old release name. | Use an explicit root allowlist and derive the package version from the project. Reject a conflicting explicit version. |
| High | Two app instances could choose the same history filename before either published its file. One entry would fail. A system calendar that differs from the Gregorian calendar could also produce names the history parser interpreted incorrectly. | Commit without overwrite and retry a collision using the already-written temporary file. Use invariant dates. |
| Medium | Starting the app rewrote readable settings. A write failure could block capture even when the configuration was valid. | Read settings at startup. Write on Save or an explicit recovery reset. |
| Medium | A capability probe that exceeded its output limit could remain alive until the full timeout. | Stop the child when a bounded output reader fails. Test that a noisy child is rejected before the timeout. |

History enumeration now reuses a case-insensitive extension set instead of allocating an array and lowercasing the extension for every file. Publishing and runtime documentation describe the current release.

Review paths covered application startup and settings persistence, screenshot and video entry points, FFmpeg startup and teardown, preview/export ownership, capture history, and packaging. This was a source and automated review of those paths. Windows desktop interactions, driver-specific WGC behavior, real audio devices, and long-duration recording performance still require runtime testing.

The Linux release remains the final stage. The first target is Wayland with XDG Desktop Portal. The next implementation milestone is a portal-authorized screen stream that exposes usable pixels to the shared capture workflow. The portal returns a scoped PipeWire descriptor, so runtime acceptance must exercise the actual session lifecycle, consent, and cancellation. Global shortcuts need a separate portal session.

- [ScreenCast portal](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html)
- [Global Shortcuts portal](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.GlobalShortcuts.html)
