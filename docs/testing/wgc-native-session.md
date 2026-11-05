# WGC native monitor session — S3.1

## Scope

Implemented a request-owned native monitor session and frame coordinator. Production composition still uses GDI; this batch does not publish a new portable release or activate WGC for users.

- Monitor handles are resolved from a fresh enumeration and matched against physical geometry and scale.
- WGC uses a free-threaded frame pool, disables cursor capture before starting, and validates frame geometry and timestamps.
- Direct3D staging readback explicitly submits the GPU copy, polls without blocking Map, observes cancellation, copies padded rows into an independent opaque BGRA buffer, and always unmaps successful mappings.
- A linked five-second deadline covers frame acquisition. Cleanup waits for an active reader before disposing native resources; late callbacks cannot read the closed session.
- Source/capability errors are classified for the routing policy. Cancellation, invalid data, topology changes and allocation failures do not silently select GDI.
- English and Russian diagnostics were added.

## Verification

`scripts/build.ps1` completed locked restore, Release solution build and all **461 tests** (121 Core, 340 App), with zero warnings/errors. Log: `TestResults/wgc-lifetime-build.log`.

The 18 new cases use injected sessions and synthetic buffers: fresh/empty frame events, timeout, cancellation before acquisition and during readback, frame/cancel races, startup and cleanup errors, geometry rejection, row padding, opaque alpha, independent pixel ownership and invalid buffer arithmetic.

Read-only review identified missing GPU submission before nonblocking polling; the final source calls `Flush` after `CopyResource`. No further actionable lifecycle/COM ownership findings were reported.

## Limits and next step

No native capture, GPU/device initialization, GUI, clipboard or audio APIs were executed. The native smoke project was compiled only. Synthetic tests cannot prove driver behavior, real monitor topology or native resource cleanup.

The deadline is cooperative: synchronous driver/COM initialization or cleanup cannot be forcibly interrupted safely. Do not interpret it as protection against a hung graphics driver.

S3.2 remains: desktop composition, monitor synchronization and SDR/HDR capability policy. S4 will wire the native source into the screenshot workflow and prepare the portable release. Video WGC remains a subsequent stage.
