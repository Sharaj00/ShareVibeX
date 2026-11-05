using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Tests;

public sealed class RecordingCommandTests
{
    [Fact]
    public void OpusOutputRequiresSupportedSampleRateBeforeStartingRecorder()=>Assert.ThrowsAny<ArgumentException>(()=>
        FfmpegCommandBuilder.Build(new(){Container=VideoContainer.Matroska,AudioEncoder=AudioEncoder.Libopus,AudioSampleRate=44100},
            new(0,0,100,100),true,"test.partial.mkv",["-f","f32le","-ar","48000","-ac","2","-i",@"\\.\pipe\audio"]));
    private const string Output = @"C:\My clips\test.partial.mp4";
    private static readonly PixelRect Region = new(-1920, -51, 1919, 1079);

    [Fact]
    public void OriginalPadsOddDimensionsWithoutChangingCaptureCoordinates()
    {
        var command = FfmpegCommandBuilder.Build(new(), Region, false, Output);
        Assert.Equal(1920, command.Width); Assert.Equal(1080, command.Height);
        AssertPair(command, "-offset_x", "-1920"); AssertPair(command, "-offset_y", "-51");
        AssertPair(command, "-video_size", "1919x1079");
        Assert.Contains("-an", command.Arguments); Assert.Equal(Output, command.Arguments.Last());
        Assert.DoesNotContain("-c:a", command.Arguments);
    }

    [Theory]
    [InlineData(ScaleMode.Percentage, 50, 1.5, 960, 540)]
    [InlineData(ScaleMode.DivideBy, 100, 1.5, 1280, 720)]
    public void ScaleIsIndependentOfPhysicalCapture(ScaleMode mode, double percent, double factor, int width, int height)
    {
        var result = VideoScaling.Calculate(new(0, 0, 1920, 1080), new() { ScaleMode = mode, ScalePercent = percent, DivideBy = factor });
        Assert.Equal(width, result.Width); Assert.Equal(height, result.Height);
    }

    [Fact]
    public void TargetCanSpecifyOnlyHeightAndKeepsAspect()
    {
        var result = VideoScaling.Calculate(new(0, 0, 1920, 1080), new() { ScaleMode = ScaleMode.TargetSize, TargetWidth = 0, TargetHeight = 720 });
        Assert.Equal(1280, result.Width); Assert.Equal(720, result.Height);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidScaleIsRejected(double factor) => Assert.ThrowsAny<ArgumentException>(() =>
        VideoScaling.Calculate(Region, new() { ScaleMode = ScaleMode.DivideBy, DivideBy = factor }));

    [Fact]
    public void AudioIsExplicitAndDoesNotAlterSilentCommand()
    {
        Assert.ThrowsAny<ArgumentException>(() => FfmpegCommandBuilder.Build(new(), Region, true, Output));
        var command = FfmpegCommandBuilder.Build(new(), Region, true, Output, ["-f", "f32le", "-ar", "48000", "-ac", "2", "-i", @"\\.\pipe\audio"]);
        AssertPair(command, "-c:a", "aac"); Assert.Contains("1:a:0", command.Arguments);
        Assert.DoesNotContain("-an", command.Arguments);
    }

    [Theory]
    [InlineData("-vf scale=1:1")] [InlineData("-i input.mp4")] [InlineData("-y extra.mp4")]
    [InlineData("-f tee")] [InlineData("-c:a aac")] [InlineData("-g 60 extra.mp4")]
    public void AdditionalArgumentsCannotOverrideManagedOptions(string text) => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { AdditionalArguments = text }, Region, false, Output));

    [Fact]
    public void AdditionalEncoderOptionsAreSeparateArguments()
    {
        var command = FfmpegCommandBuilder.Build(new() { AdditionalArguments = "-g 60 -metadata \"title=My clip\"" }, Region, false, Output);
        AssertPair(command, "-metadata", "title=My clip"); AssertPair(command, "-g", "60");
    }

    [Fact]
    public void CustomTemplateExpandsValuesWithoutSplittingOutputPath()
    {
        var command = FfmpegCommandBuilder.Build(new() { UseCustomCommand = true }, Region, false, Output);
        Assert.Equal(Output, command.Arguments.Last()); Assert.Contains("-an", command.Arguments);
        Assert.DoesNotContain(command.Arguments, arg => arg.Contains('$'));
    }

    [Theory]
    [InlineData("-i desktop -an extra.mp4 $output$")]
    [InlineData("-i desktop -an $unknown$ $output$")]
    [InlineData("-i desktop -an -t $duration$ $output$")]
    [InlineData("-i desktop -c:a aac $output$")]
    [InlineData("-i desktop -an -f tee $output$")]
    [InlineData("-i $output$ -an $output$")]
    [InlineData("-i desktop -an $output$ -f mp4")]
    public void UnsafeOrAmbiguousCustomCommandsAreRejected(string template) => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { UseCustomCommand = true, CustomCommand = template }, Region, false, Output));

    [Fact]
    public void CustomScalingCannotBeSilentlyIgnored() => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { UseCustomCommand = true, CustomCommand = "-i desktop -an $output$", ScaleMode = ScaleMode.Percentage, ScalePercent = 50 }, Region, false, Output));

    [Theory]
    [InlineData(@"\\server\My clips\capture.mp4")]
    [InlineData(@"C:\My clips\")]
    [InlineData("title=He said \"hello\"")]
    [InlineData(@"C:\O'Brien\ffmpeg.exe")]
    [InlineData("title='quoted clip'")]
    public void PreviewQuotingAndParsingPreserveWindowsValues(string value)
    {
        var parsed = FfmpegArguments.Parse(FfmpegArguments.Display(new[] { value }));
        Assert.Equal(new[] { value }, parsed);
    }

    [Fact]
    public void ScalingPlaceholderInMetadataDoesNotApplyScaling() => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { UseCustomCommand = true, ScaleMode = ScaleMode.Percentage, ScalePercent = 50,
            CustomCommand = "-i desktop -metadata title=$scale_filter$ -an $output$" }, Region, false, Output));

    [Theory]
    [InlineData("-i desktop -metadata title=$duration$ -an $output$")]
    [InlineData("-t $duration$ -i desktop -an $output$")]
    [InlineData("-i desktop -an -t $duration$ -t 50 $output$")]
    public void SelectedDurationMustBeAUniqueOutputOption(string template) => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { UseCustomCommand = true, DurationSeconds = 10, CustomCommand = template }, Region, false, Output));

    [Fact]
    public void SelectedDurationAppearsInOutputOptions()
    {
        var command = FfmpegCommandBuilder.Build(new() { UseCustomCommand = true, DurationSeconds = 10,
            CustomCommand = "-i desktop -an -t $duration$ $output$" }, Region, false, Output);
        AssertPair(command, "-t", "10");
    }

    [Fact]
    public void AudioCustomCommandCannotMapOnlyVideoAndSilentlyDropSound() => Assert.ThrowsAny<ArgumentException>(() =>
        FfmpegCommandBuilder.Build(new() { UseCustomCommand = true,
            CustomCommand = "-i desktop $audio_input$ -map 0:v:0 -c:v libx264 -c:a aac $output$" }, Region, true, Output,
            ["-f", "f32le", "-ar", "48000", "-ac", "2", "-i", @"\\.\pipe\audio"]));
    private static void AssertPair(FfmpegCommand command, string key, string value)
    {
        var index = command.Arguments.ToList().IndexOf(key);
        Assert.True(index >= 0); Assert.Equal(value, command.Arguments[index + 1]);
    }
}
