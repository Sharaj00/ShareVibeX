# Frame, video timing footer and true-pause investigation — 0.5.4

Latest user steering: first test true process pause instead of continuous background pre-roll. This authorizes a scoped synthetic FFmpeg process experiment, not desktop/microphone recording or silently substituting pre-roll.

Completed:
1. Replace four2px frame windows with one normal-sized excluded native hollow region. Region mask must precede Show and be reapplied after actual DPI sizing. Failed masking/exclusion never shows an interior-blocking surface. Headless mask seams and one-window lifetime tests pass. Exact native render/capture behavior remains user acceptance.
2. Video.ShowVideoTimings persisted defaulttrue checkbox. Time footer after action row, reserve its placement height to preserve anchor when hidden. Headless settings/footer tests pass; A/B time labels remain absent.
3. Genuine NtSuspendProcess/NtResumeProcess test of only own synthetic FFmpeg child, no GUI/device/capture/clipboard. Zero paused CPU confirms OS pause; CFR video included waiting and A/V durations diverged. Control baseline was normal. Report docs/testing/2026-11-03-process-pause-spike.md. Prepare recording is intentionally NOT implemented/enabled because the tested mechanism failed the intended semantics; user chose this spike first and did not approve pre-roll substitution.
4. One narrow independent source review: no material findings. Final Release suite302/302 (91core+211app),0warnings/errors. Local0.5.4 portable package with source/manual/licenses; prior archives/data preserved. No remote publication.

Next prepared-recorder work requires coordinated input/backpressure/timestamp gating design; do not reintroduce OS suspension as a proven media pause. Existing production Pause remains segmented finalization/resume and is unchanged.
