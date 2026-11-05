# Unreleased recording Pause/Resume and live microphone — increment 3

This continues the approved staged inline-video design. The working source is ahead of the portable0.4.1 ZIP; that published parser-fix archive is preserved. This is not the complete playback/editor feature release.

## Connected behavior

- Runtime actions are Stop, Pause/Resume, Microphone and Cancel vector icons with short tooltips. The paused state remains a recording-owned session, including tray/hotkey Stop and topology-change handling.
- Pause gracefully finalizes the current segment and releases its audio sources; Resume opens the configured endpoints into the next unique segment. Paused wall time is excluded from cumulative media duration and the configured duration limit.
- Stop while paused assembles existing media without opening a new recording/device. Resume startup failure keeps earlier segments and permits retry or Stop.
- Native capture uses a paced stereo PCM channel even with both inputs off; no playback/mic device is opened for those off inputs. Live microphone activation is serialized; failure preserves the earlier state. Deactivation removes its buffer and releases capture. An unconfirmed device-release failure fails the stream rather than continuing silently.
- Changes to microphone selection while paused update the choice for Resume and do not open hardware. Completely silent sessions omit the internal audio track from their final output; otherwise silent intervals remain on the shared timeline.
- Ordered segments use frame-derived video duration when available. Multi-segment assembly resets video/audio timestamps, pads/trims audio to segment duration and re-encodes through the selected encoder/settings. Compatible single-segment output can copy directly; stripping a silent audio track uses a remux. No automatic encoder fallback occurs.
- Cancel interrupts a pending resume or assembly, closes processes/devices and deletes only owned workspace paths. Errors retain original segments and recovery metadata.

## Explicit limits

Custom FFmpeg command mode keeps single-run recording but requires structured encoding settings for Pause. DirectShow retains its fixed source behavior and cannot claim live native microphone mixing. Very large assembly argument lists are rejected recoverably before launch, keeping original segments. Final assembly may require additional processing time. Native A/V synchronization, permissions, endpoint formats, hardware initialization, capture exclusion/DPI and real media compatibility remain user checks.

## Verification boundary

Tests use fake input devices/processes, synthetic audio, temporary files, headless Avalonia and ordinary hidden console-process protocol tests. No actual app, screen/compositor capture, system FFmpeg, device enumeration/capture or live clipboard has been executed by the agent. Source review and a final internal suite are recorded in the plan ledger; native behavior is not marked accepted.

Final verification: scripts/build.ps1 passed241 tests (82core+159app), with0warnings/errors. One independent economical source review found two resource-cleanup failures: a candidate microphone-disposal exception could hold a semaphore, and a process-termination exception could skip job/process disposal. Both were reproduced RED and corrected GREEN, including a plain console-child job-lifetime regression. No minor findings were reported for this increment. Runtime timing/media/hardware acceptance remains separate.

Next approved increment: real in-place A/V playback and seeking, then A–B edits, immutable video-file clipboard export, final Save and recovery/cleanup UI. Stopping currently retains a result/path for that future preview handoff.
