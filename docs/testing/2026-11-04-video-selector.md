#0.12.3 selector move / encoder menu start

Common capture preference moved mechanically from CaptureSettingsPage to VideoSettingsPage Encoding. SettingsWindow persists the same AppSettings.CaptureBackend; screenshot/video runtime routes are unchanged. Legacy automation ID retained. Headless English/Russian regression saves the selected method from Video, checks it is absent from Capture, and preserves other settings.

Encoder menu work starts with disabled unsupported Tune and software placebo preset. Switching to QSV resets incompatible preset/tune and rejects software-only presets in the menu; covered headlessly. Full encoder expansion/profile persistence and FFmpeg option compatibility remain planned, not claimed complete.

Locked restore/Release build zero warnings/errors;509 tests (131 Core/378 App) passed. Log: TestResults/video-selector-encoder-build.log. User explicitly confirmed all four formerly active acceptance tasks; no new native capture/GPU/audio/clipboard APIs were run by the agent.
