using ShareVibeX.Core.Capture;
using System.Globalization;

namespace ShareVibeX.Core.Recording;

public sealed record ScaledVideo(int ContentWidth, int ContentHeight, int Width, int Height, string Filter);

public static class VideoScaling
{
    public static ScaledVideo Calculate(PixelRect region, RecordingOptions options)
    {
        if (region.IsEmpty || region.Width > 32768 || region.Height > 32768)
            throw new ArgumentException(Loc.T("Capture dimensions must be between 1 and 32768 pixels."));
        if (!Enum.IsDefined(options.ScaleMode) || !Enum.IsDefined(options.ScaleAlgorithm))
            throw new ArgumentException(Loc.T("Unknown scaling mode or algorithm."));
        double width = region.Width, height = region.Height;
        switch (options.ScaleMode)
        {
            case ScaleMode.Percentage:
                Positive(options.ScalePercent); width *= options.ScalePercent / 100; height *= options.ScalePercent / 100; break;
            case ScaleMode.DivideBy:
                Positive(options.DivideBy); width /= options.DivideBy; height /= options.DivideBy; break;
            case ScaleMode.TargetSize:
                if (options.TargetWidth < 0 || options.TargetHeight < 0 || options.TargetWidth + (long)options.TargetHeight == 0)
                    throw new ArgumentException(Loc.T("Specify a positive target width and/or height."));
                if (options.KeepAspectRatio)
                {
                    var factor = options.TargetWidth == 0 ? (double)options.TargetHeight / region.Height :
                        options.TargetHeight == 0 ? (double)options.TargetWidth / region.Width :
                        Math.Min((double)options.TargetWidth / region.Width, (double)options.TargetHeight / region.Height);
                    width *= factor; height *= factor;
                }
                else
                {
                    if (options.TargetWidth == 0 || options.TargetHeight == 0)
                        throw new ArgumentException(Loc.T("Both target dimensions are required when aspect ratio is unlocked."));
                    width = options.TargetWidth; height = options.TargetHeight;
                }
                break;
        }
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1 || width > 16384 || height > 16384)
            throw new ArgumentException(Loc.T("Output dimensions must be between 1 and 16384 pixels."));
        var contentWidth = (int)Math.Round(width, MidpointRounding.AwayFromZero);
        var contentHeight = (int)Math.Round(height, MidpointRounding.AwayFromZero);
        var filter = options.ScaleMode == ScaleMode.Original ? "" :
            FormattableString.Invariant($"scale={contentWidth}:{contentHeight}:flags={options.ScaleAlgorithm.ToString().ToLowerInvariant()},");
        filter += "pad=ceil(iw/2)*2:ceil(ih/2)*2,format="+(options.Encoder==VideoEncoder.Ffv1?"bgr0":options.IsAnimation?"rgba":"yuv420p");
        return new(contentWidth, contentHeight, (contentWidth + 1) & ~1, (contentHeight + 1) & ~1, filter);
    }
    private static void Positive(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentException(Loc.T("Scale must be a finite positive number."));
    }
}
