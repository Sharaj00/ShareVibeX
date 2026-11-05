# ShareVibeX

ShareVibeX is a portable screen capture and recording tool for Windows 11. It was built in C# and Avalonia with AI-assisted development.

[Download the Windows x64 release](https://github.com/Sharaj00/ShareVibeX/releases/latest)

## What it does

- **One capture shortcut.** Select a live region and choose screenshot editing or video recording. Press Enter or Ctrl+C to copy a fresh screenshot directly.
- **In-place screenshot editing.** Draw, annotate with text and arrows, and outline, fill, or blur shapes. Save as PNG, JPEG, or WebP.
- **Region recording.** Record with system audio, microphone, both, or neither. Configure FFmpeg encoders, containers, quality, frame rate, and scaling. Animated export supports GIF, WebP, and APNG.
- **In-place video preview.** Pause, seek, step through frames, and keep or remove an A–B range. Copy the edited file to the clipboard or save it to a folder.
- **Portable preferences.** A tray menu, English and Russian interfaces, Windows system and AMOLED themes, configurable capture outlines, and optional capture history.

Windows Graphics Capture and GDI are available for screenshots and structured recordings. Eligible single-monitor GDI and SDR WGC recordings can follow a movable region without restarting the encoder. In true exclusive fullscreen, the capture shortcut copies the monitor image directly.

## Getting started

1. Extract the entire ZIP into a writable folder and run `ShareVibeX.App.exe`. The app opens in the system tray.
2. Press **PrintScreen** or choose **Capture region** from the tray menu. To change the shortcut, click the current combination in **Settings → General**.
3. To record video, use a compatible FFmpeg build. **Settings → Capture → Media** shows the detected executable. Click the path to select a different one and use **Test** to inspect its capabilities.

Screenshots require no FFmpeg. The portable Windows build includes the .NET runtime. FFmpeg is discovered separately and is not bundled. The [FFmpeg download page](https://ffmpeg.org/download.html) lists available builds.

Settings live in `settings.json` next to the executable. Older `data/settings.json` profiles are read if the new file is absent. Keep the `libs` directory beside the executable. The default output folder is `Screenshots` in your Windows Pictures folder. General settings control monthly subfolders and whether Copy and Save add results to History.

## Build from source

Use PowerShell 7 and the .NET SDK pinned in [global.json](global.json).

```powershell
./scripts/build.ps1
./scripts/publish.ps1
```

The build runs the automated tests. Publishing creates the portable Windows x64 ZIP under `dist`. See [recording settings](docs/recording-settings.md) and the [Windows verification checklist](docs/testing/WGC-video-manual.md).

## Linux

Linux support is planned. The first target is Wayland through XDG Desktop Portal and PipeWire. See [LINUX.md](LINUX.md) for the implementation and release plan.

## License

ShareVibeX is distributed under the [GNU General Public License v3.0](LICENSE). See [third-party notices](THIRD-PARTY-NOTICES.md) for dependency and artwork attribution.
