# ShareVibeX — user verification

Automated checks use synthetic media, injected processes and headless UI. Real capture, encoding/decoding, audio, clipboard and GPU behavior remain user acceptance.

1. Exit the older instance, extract the entire portable folder and preserve previous data/media. Copy desired settings.json if needed. Startup must remain tray-only.
2. In Capture → Video choose the common Automatic/WGC/GDI method. Verify screenshots/editor/quick copy remain correct. WGC SDR/HDR and mixed-DPI/topology behavior follow the existing method policy.
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
2. Repeat with and without sound. Stop and verify that the video follows the border throughout the drag, with continuous sound and no segment join. WGC, custom commands, and regions spanning multiple monitors keep a fixed capture region.
3. Trim a recording and Copy it. Check that the editor closes, the clipboard file remains available, and History contains the edited result when enabled.
4. Repeat with Save and verify the chosen file, clipboard, and History. Canceling the Save dialog should keep the editor open.

No FFmpeg is bundled or installed by ShareVibeX. Actual codec/hardware availability, media quality, timing, resource use and receiving-app behavior are not proven by metadata or synthetic tests.

## Settings and appearance

1. In General set an output folder. Check screenshot Save, video Save and History with and without Create month folder. Save inside the proposed folder and verify there is only one file.
2. In Capture → Video verify that automatic FFmpeg discovery shows the executable path. Click the path to choose a different executable. Test should only probe capabilities. With no FFmpeg, Install opens the official download page.
3. In Capture → Selection → Frame try each line type, a custom color and reduced opacity. Check the selection and recording frames. Turn off the recording frame and verify that capture still works.
4. Switch between system light, system dark and AMOLED. Verify that the camera icon is dark in light and white in dark and AMOLED.