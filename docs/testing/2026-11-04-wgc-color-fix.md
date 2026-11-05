# WGC color-mode mapping fix —0.12.1

SVX-023: SourceName contains native WCHAR[32], but StructLayout defaulted to ANSI. A synthetic unmanaged buffer containing the UTF-16 Windows display name reproduced truncation to a single backslash. Monitor-name matching then rejected otherwise valid color metadata. Explicit CharSet.Unicode fixes unmarshalling; ReadSourceName is shared by the reader and regression.

The regression failed against the original definition and passed after the charset correction. Locked restore, Release solution build and480 tests (121 Core/359 App) passed with zero warnings/errors; TestResults/wgc-color-fix-build.log. No actual display/color/GPU/capture queries were executed. HDR/unknown-color fallback policy is unchanged. Native user acceptance is still pending.

Reference: Windows SDK wingdi.h DISPLAYCONFIG_SOURCE_DEVICE_NAME; Microsoft Learn https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_source_device_name
