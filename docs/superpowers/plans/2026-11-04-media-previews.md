# Media preview refinement — 2026-11-04

User clarified completion notices must preview captured media. History must show image/video thumbnails, month tree and newest capture first, and open promptly.

Implementation responsibilities: bounded shared thumbnail loader; asynchronous history enumeration and incremental/lazy thumbnail loading with cancellation; capture timestamp parsing from filenames for ordering; month nodes descending. Notifications carry a bounded in-memory screenshot preview even with history disabled, or load a static video frame from the durable exported file. No hidden screenshot archive. Preserve capture exclusion, recording suppression and result ownership cleanup.

Verification: synthetic media/headless/filesystem tests only; native GUI/capture/audio/clipboard acceptance remains user-owned. Publish next portable release with manual checklist and tracking updates.
