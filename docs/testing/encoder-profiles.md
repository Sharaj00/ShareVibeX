# Encoder-specific profiles — internal batch

RecordingOptions.SwitchEncoder snapshots outgoing encoding fields into a copied dictionary and restores target values (or default encoding values on first use). Profiles contain rate control, quality, bitrate, preset, tune and additional encoding arguments. Shared capture/audio/scale/container values remain unchanged. Active top-level fields remain compatible with existing JSON; switching captures their latest values, even if a stored active entry is older.

UI encoder selection now uses Apply under the existing syncing guard, restoring all profile fields without recursive change events. Reset recording options clears profile storage. Inactive profiles are validated on load/save as well as active fields, including encoder enum/preset/tune/ranges/null entries. Unknown profiles are rejected, not silently dropped.

Tests cover software/hardware round trips, non-mutation, shared-option preservation, portable JSON save/load, legacy/default options, invalid inactive profiles and headless UI restore of placebo/film after QSV selection. No native hardware or FFmpeg encoding was executed.

Full locked restore/Release suite:513 tests passed (135 Core/378 App), zero warnings/errors. Log: TestResults/encoder-profiles-build.log. Published portable remains0.12.3; new codecs, per-build encoder help validation and animation/container/export compatibility remain planned.
