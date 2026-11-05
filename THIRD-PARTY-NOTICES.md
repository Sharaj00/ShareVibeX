# Third-party notices

ShareVibeX's application code is GPL-3.0-or-later. Third-party components retain their original licenses.

The portable application uses Avalonia 12.1.4 including Avalonia.Controls.ColorPicker (MIT), MicroCom.Runtime (MIT), SkiaSharp with native Skia (MIT and upstream notices), HarfBuzzSharp with native HarfBuzz (MIT and upstream notices), ANGLE native libraries (BSD-style and bundled upstream notices), and .NET 10 (MIT and upstream notices). Some platform assemblies from Avalonia.Desktop are shared across targets; the packaged Windows app uses the Windows backend. Tmds.DBus.Protocol (MIT) is a transitive desktop dependency.

Exact transitive versions are pinned in `src/ShareVibeX.App/packages.lock.json`. Full available license/notice files, package metadata, and upstream license texts are included in `third-party-licenses`. The .NET runtime's own LICENSE/ThirdPartyNotices files are included in the self-contained output as supplied by Microsoft.

Windows audio recording uses NAudio.Wasapi 2.3.0 and NAudio.Core 2.3.0 (MIT, Mark Heath). Package metadata and the upstream license from package repository commit c89fee940ee6f8d7374d18714a6b85d8b7a18ab0 are included in third-party-licenses/NAudio-2.3.0.

The WGC build foundation adds Windows SDK .NET targeting package 10.0.19041.57 and its Microsoft.Windows.SDK.NET / WinRT.Runtime projections (Microsoft Windows SDK package terms; the linked SDK license and package metadata are included in third-party-licenses/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57). The targeting pack is a build dependency; required managed runtime projections are copied into self-contained output. Windows system graphics DLLs are supplied by Windows, not redistributed with this app.

Direct3D bindings use Vortice.Direct3D11, Vortice.DXGI and Vortice.DirectX 3.8.3 (MIT), Vortice.Mathematics 2.1.0 (MIT), and their pinned SharpGen.Runtime / SharpGen.Runtime.COM 2.4.2-beta dependencies (MIT). Exact package metadata, attribution and upstream license texts from each package's repository commit are included under third-party-licenses. These bindings do not themselves enable WGC capture before the native implementation is integrated.

Development-only xUnit and Microsoft.NET.Test.Sdk dependencies are not shipped in the portable app. Tests use real Windows capture and clipboard only in the opt-in Windows smoke harness.

Reference projects, not bundled application dependencies at this stage:

- ShareX: https://github.com/ShareX/ShareX — GPL-3.0; no source copied in stage 1.
- FFmpeg: https://ffmpeg.org/ — external recording tool, not bundled; license depends on the user's selected build.

## Camera icon

Solar Camera Broken by 480 Design, supplied as an SVG Repo asset. ShareVibeX converts the artwork to ICO and recolors the tray variants. The artwork is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). See the [Solar collection and attribution](https://icon-sets.iconify.design/solar/).
