using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Tests;

public sealed class FfmpegProbeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WgcSourceUsesActualHelpAndDoesNotRequireGdigrab(bool stderr)
    {
        var runner=new FakeRunner{FilterOutput=" .. gfxcapture |->V\n .. hwdownload V->V\n .. format V->V\n .. setpts V->V\n .. fps V->V",FiltersOnStandardError=stderr};
        var c=(await new FfmpegProbe(runner).TestAsync("ffmpeg.exe")).Capabilities with{Inputs=new HashSet<string>{"lavfi"}};
        Assert.Empty(c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture));
        Assert.Contains("gdigrab",c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.Gdi));
        Assert.Equal(5,runner.Calls.Count);Assert.Equal(new[]{"-hide_banner","-h","filter=gfxcapture"},runner.Calls.Last());
        Assert.DoesNotContain(runner.Calls.SelectMany(a=>a),a=>a is "-i" or "-list_devices");
    }
    [Fact] public async Task FailedOptionalWgcHelpPreservesGdiCapabilities()
    {
        var runner=new FakeRunner{FilterOutput=" .. gfxcapture |->V\n .. pad V->V\n .. format V->V",FailHelp=true};
        var c=(await new FfmpegProbe(runner).TestAsync("ffmpeg.exe")).Capabilities;
        Assert.NotNull(c.GfxCaptureProbeError);Assert.Empty(c.Missing(new(),false));
        Assert.NotEmpty(c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture));
    }
    [Fact] public async Task MissingHelpOptionsAreNotInferredFromVersionOrDescription()
    {
        var runner=new FakeRunner{FilterOutput=" .. gfxcapture |->V",HelpOutput="Filter gfxcapture\n monitor_idx <int> ..FV....... monitor\n hmonitor is unsupported\n"};
        var c=(await new FfmpegProbe(runner).TestAsync("ffmpeg.exe")).Capabilities;
        Assert.Contains("gfxcapture.hmonitor",c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture));
        Assert.Contains("hwdownload",c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture));
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend.Automatic));
    }
    [Fact] public async Task CancellationDuringOptionalHelpIsNotSuppressed()
    {
        using var cancellation=new CancellationTokenSource();
        var runner=new FakeRunner{FilterOutput=" .. gfxcapture |->V",BeforeHelp=()=>cancellation.Cancel()};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new FfmpegProbe(runner).TestAsync("ffmpeg.exe",cancellation.Token));
    }
    [Fact]
    public async Task ProbeReadsCapabilitiesWithoutOpeningCaptureDevices()
    {
        var runner = new FakeRunner();
        var result = await new FfmpegProbe(runner).TestAsync("ffmpeg.exe");
        Assert.StartsWith("ffmpeg version 8.0", result.Capabilities.Version);
        Assert.Contains("gdigrab", result.Capabilities.Inputs); Assert.Contains("libx264", result.Capabilities.Encoders);
        Assert.Contains("scale", result.Capabilities.Filters);
        Assert.Empty(result.Capabilities.Missing(new(), true));
        Assert.Equal(4, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls.SelectMany(args => args), arg => arg is "-i" or "-list_devices");
    }
    [Fact]
    public async Task NonFfmpegExecutableIsRejected() => await Assert.ThrowsAsync<InvalidDataException>(() =>
        new FfmpegProbe(new FakeRunner { BadVersion = true }).TestAsync("wrong.exe"));
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProbeReadsTwoFlagFilterRowsAndDoesNotInventMissingFilters(bool standardError)
    {
        var runner = new FakeRunner
        {
            FilterOutput = "Filters:\r\n  T.. = Timeline support\r\n  .S. = Slice threading\r\n  ------\r\n .. format            V->V       Convert pixel format\r\n .. pad               V->V       Pad input video\r\n .S scale             V->V       Scale video\r\n TS drawbox           V->V       Draw a box\r\n",
            FiltersOnStandardError = standardError
        };
        var capabilities = (await new FfmpegProbe(runner).TestAsync("ffmpeg.exe")).Capabilities;
        Assert.Contains("pad", capabilities.Filters);
        Assert.Contains("format", capabilities.Filters);
        Assert.Contains("scale", capabilities.Filters);
        Assert.Contains("drawbox", capabilities.Filters);
        Assert.DoesNotContain("=", capabilities.Filters);
        Assert.Empty(capabilities.Missing(new() { ScaleMode = ScaleMode.Percentage }, true));
    }
    [Fact]
    public async Task FailedCapabilityQueryIsReported() => await Assert.ThrowsAsync<IOException>(() =>
        new FfmpegProbe(new FakeRunner { FailEncoders = true }).TestAsync("broken.exe"));
    [Fact]
    public async Task CancellationStopsFurtherProbes()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FfmpegProbe(new FakeRunner()).TestAsync("ffmpeg.exe", cancellation.Token));
    }
    private sealed class FakeRunner : ICommandRunner
    {
        public bool BadVersion, FailEncoders,FailHelp;
        public string? HelpOutput;
        public Action? BeforeHelp;
        public string? FilterOutput;
        public bool FiltersOnStandardError;
        public List<IReadOnlyList<string>> Calls = [];
        public Task<ToolResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Calls.Add(arguments);
            if(arguments.Last()=="filter=gfxcapture"){BeforeHelp?.Invoke();token.ThrowIfCancellationRequested();}
            var result = arguments.Last() switch
            {
                "-version" => BadVersion ? "Other tool" : "ffmpeg version 8.0 Copyright FFmpeg developers\nconfiguration: test",
                "-devices" => "Devices:\n D  gdigrab         GDI API\n DE dshow           DirectShow\n  E dummy           output only",
                "-encoders" => "Encoders:\n V....D libx264 H.264\n A....D aac AAC\n V....D h264_nvenc NVENC",
                "-filters" => FilterOutput ?? "Filters:\n ... scale V->V\n ... pad V->V\n ... format V->V",
                "filter=gfxcapture" => HelpOutput??string.Join("\n",new[]{"hmonitor","capture_cursor","max_framerate","crop_left","crop_top","crop_right","crop_bottom","width","height","resize_mode","output_fmt"}.Select(o=>"   "+o+" <int> ..FV....... option")),
                _ => throw new InvalidOperationException()
            };
            var stderr = FiltersOnStandardError && arguments.Last() is "-filters" or "filter=gfxcapture";
            return Task.FromResult(new ToolResult(FailEncoders && arguments.Last() == "-encoders" || FailHelp&&arguments.Last()=="filter=gfxcapture" ? 1 : 0, stderr ? "" : result, stderr ? result : ""));
        }
    }
}
