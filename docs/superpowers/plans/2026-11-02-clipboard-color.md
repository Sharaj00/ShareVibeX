# Clipboard color compatibility

The user reports correct direct chat paste but black Windows clipboard history and image export. Desktop and live clipboard verification remain assigned to the user.

## Investigation

Avalonia 12.1.4 `OleDataObjectHelper.WriteDataToGdi` calls `CreateCompatibleBitmap(desDc, ...)` on a newly created memory DC. Microsoft documents that this produces a monochrome bitmap. PNG and DIB follow separate conversion paths. This is a source-confirmed compatibility defect matching the reported symptom; runtime confirmation remains manual.

References:
- https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Windows/Avalonia.Win32/OleDataObjectHelper.cs
- https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatiblebitmap

## Approved scope and implementation

This is a correction of already approved clipboard behavior, requiring no new product decision. Preserve the Avalonia clipboard adapter for other platforms. On Windows, eagerly publish PNG and a 24-bit bottom-up CF_DIB from the same image, letting Windows synthesize CF_BITMAP from the color DIB. Allocate all memory before opening/emptying the clipboard; transfer ownership only after each successful SetClipboardData; release unpublished handles on every failure. Retry a busy clipboard without blocking the UI thread.

1. Add regression coverage for color, row order, odd-width padding and source stride; decode DIB independently and compare with PNG.
2. Implement isolated Windows payload encoding/publication and connect the capture copy action.
3. Run internal tests, independent source review, build portable 0.2.3, and provide manual checks. Never launch the app or access the live clipboard.
