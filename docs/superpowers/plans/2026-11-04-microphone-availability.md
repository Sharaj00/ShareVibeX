# Approved microphone availability fix — 0.11.2

- [x] Add injectable active-input/default/selected-device availability policy using existing audio enumeration, without opening an endpoint.
- [x] Disable/uncheck unavailable microphone in video-ready controls; localized tooltip remains visible on disabled button. Refresh on entry and periodically, recheck before Record.
- [x] Gate runtime microphone control and check again before enabling. Missing-device rejection happens before recorder startup; source failures do not force Settings open.
- [x] Cover device absence, configured/default mismatch, reconnect, errors/cancellation, English/Russian panels and startup rejection with fakes/headless tests.
- [x] Full Release build0 warnings/errors;415 tests passed. Native device/capture acceptance remains user-owned.
- [x] Portable package/source/archive consistency verified: ZIP integrity, three shipped DLL hashes, nested source/tracking files, current manual/version, no FFmpeg or portable settings; SHA256 sidecar generated.
