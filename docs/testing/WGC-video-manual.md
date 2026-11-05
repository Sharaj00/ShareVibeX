# ShareVibeX — user verification

Automated checks use synthetic media, injected processes and headless UI. Real capture, encoding/decoding, audio, clipboard and GPU behavior remain user acceptance.

1. Exit the older instance, extract the entire portable folder and preserve previous data/media. Copy desired settings.json if needed. Startup must remain tray-only.
2. In Capture → Media choose the common Automatic/WGC/GDI method. Verify screenshots/editor/quick copy remain correct. WGC SDR/HDR and mixed-DPI/topology behavior follow the existing method policy.
3. Choose GIF, Animated WebP or APNG. The container control should be hidden; the export extension must match the chosen encoder. loop0 means infinite (GIF repeat count/APNG play count). Verify WebP quality/lossless and compression controls; switch away/back and save/reopen to confirm profiles.
4. Sound/microphone must be unchecked/disabled in animation selection and recording. Missing microphone must not block animation. Record/Pause/Resume/Stop; verify no audio track or audio-device error.
5. Recording creates lossless temporary segments. Check disk usage and responsiveness at the actual resolution/fps. Conversion follows Stop; completion time and file size depend on content/settings.
6. Verify paused first frame, Play, seeking/frame stepping, Keep/Remove A–B and undo. Check colors, dimensions, duration and loop behavior in the in-place preview and an external viewer. For GIF, compare a low color limit with 256 colors and try Sierra, Bayer and no dithering. Check both the finished capture and an edited export.
7. Copy publishes an immutable file; test paste into the intended receiving app and after closing/discarding the working session. Save preserves the matching extension. Invalid extensions must not overwrite an existing file.
8. Verify animation names, monthly History, first-frame thumbnails and Recycle Bin. Working-session deletion must preserve saved/history/copied files.
9. Test FFmpeg queries metadata/help only. Missing final/intermediate encoders, Matroska reader/writer, animation muxer/demuxer/decoder must fail before capture. Animated WebP requires webp_anim reader/decoder; static WebP support alone is insufficient. SVT-AV1 separately requires libsvtav1 in the selected FFmpeg.
10. Recheck ordinary video with/without audio, pause, preview/edit/Copy/Save, containers and encoder profiles in English/Russian. Report exact method, FFmpeg error/version, region, fps, scaling and audio/animation options for failures.

## Capture shortcut

1. In General, click the displayed shortcut and press a shortcut such as Ctrl+Alt+K. Repeat on the Russian and English layouts and verify the displayed shortcut is the same.
2. Verify that Esc and moving focus away leave the previous value in the editor. While assigning a shortcut, press the currently registered shortcut and verify that the screen capture does not open.
3. Save a free shortcut, reopen Settings and verify the value. Close Settings and press the shortcut to open capture.
4. Try saving a shortcut already registered by another app. Verify the error and confirm that the previous shortcut still opens capture.

## Moving the capture region and completing video export

1. Select GDI as the capture method and record a region smaller than one monitor. Hold the translucent strip above the recording buttons and drag with the left mouse button. The recording panel should follow the frame. The frame should follow before release without changing size and stop at the monitor edge.
2. Enter Video setup and drag the grip before pressing Record. Check that the grip remains visible during countdown, recording, pause and finalization. Repeat with and without sound. Stop and verify that the video follows the border throughout the drag, with continuous sound and no segment join. Repeat with Windows Graphics Capture on a single SDR monitor, both with and without the cursor. Fixed WGC, custom commands, and regions spanning multiple monitors keep a fixed capture region.
3. Trim a recording and Copy it. Check that the editor closes, the clipboard file remains available, and History contains the edited result when enabled.
4. Repeat with Save and verify the chosen file, clipboard, and History. Canceling the Save dialog should keep the editor open.

No FFmpeg is bundled or installed by ShareVibeX. Actual codec/hardware availability, media quality, timing, resource use and receiving-app behavior are not proven by metadata or synthetic tests.

## Settings and appearance

1. In General set an output folder. Check screenshot Save, video Save and History with and without Create month folder. Save inside the proposed folder and verify there is only one file.
2. In Capture → Media verify that automatic FFmpeg discovery shows the executable path. Click the path to choose a different executable. Test should only probe capabilities. With no FFmpeg, Install opens the official download page.
3. In Capture → Selection → Frame try each line type, a custom color and reduced opacity. Check the selection and recording frames. Turn off the recording frame and verify that capture still works.
4. Switch between system light, system dark and AMOLED. Verify that the camera icon is dark in light and white in dark and AMOLED.
## Fullscreen capture

1. In Endfield borderless fullscreen, press the capture shortcut and check that the live selector appears without minimizing the game. Drag a region and copy it.
2. In an exclusive Direct3D game, including at a resolution below desktop native, press the shortcut. Confirm the game stays active and the clipboard contains the monitor image. This path has no interactive selection.
3. For failures, start ShareVibeX with --diagnostics. Check data/diagnostic.log for shortcut delivery, diversion to the shortcut editor or recording, request rejection, selection, and the exclusive fullscreen path. For Endfield, press the shortcut first in the focused game and then after opening Start. Verify normal desktop region capture separately.

### Exclusive overlay feasibility

In true fullscreen exclusive mode, the game controls presentation to its display. A topmost desktop window only changes desktop window ordering. A nonactivating window also cannot take keyboard focus for the editor. Windows Fullscreen Optimizations can display an overlay by giving the desktop compositor control again. An interactive overlay that stays over true exclusive rendering would need integration with the game's rendering and input, with compatibility risks across games and anti-cheat configurations. ShareVibeX keeps direct monitor copy in this mode. [DirectX Fullscreen Optimizations](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/) and [Win32 keyboard focus](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input#keyboard-focus-and-activation).

Capturing video in the background is technically separate. Desktop Duplication supports fullscreen DirectX capture, so a predefined region and global recording shortcuts could be a standalone workflow. It would not provide live selection or editing over the game. [Desktop Duplication](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/desktop-duplication-api).

## WGC region movement study

The current gfxcapture graph fixes the crop margins when FFmpeg opens the input. The installed FFmpeg exposes crop and zmq. A synthetic sendcmd test changed the crop origin midstream while all four output frames kept the same dimensions. This verifies dynamic software cropping, with no claim about WGC performance or live drag latency.

The prototype keeps one Windows Graphics Capture monitor session open. It caches the latest monitor texture on the GPU, copies the current crop into a reusable staging texture and sends a fixed-size frame through the same raw-video pipe used by GDI. A Windows smoke check read 13 frames of 128 x 128 pixels from one pipe and detected changed pixels after moving the crop. Check frame pacing, cursor visibility, capture exclusion and audio continuity during long drags in the app.
