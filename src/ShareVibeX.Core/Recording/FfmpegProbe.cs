using System.Text.RegularExpressions;

namespace ShareVibeX.Core.Recording;

public sealed record FfmpegCapabilities(string Version, IReadOnlySet<string> Inputs, IReadOnlySet<string> Encoders, IReadOnlySet<string> Filters)
{
    public IReadOnlySet<string> GfxCaptureOptions {get;init;}=new HashSet<string>(StringComparer.Ordinal);
    public string? GfxCaptureProbeError {get;init;}
    public IReadOnlyList<string> MissingVideoSource(ShareVibeX.Core.Capture.CaptureBackend resolvedSource,bool composeMonitors=false)
    {
        if(resolvedSource==ShareVibeX.Core.Capture.CaptureBackend.Gdi)
            return Inputs.Contains("gdigrab")?[]:["gdigrab"];
        if(resolvedSource!=ShareVibeX.Core.Capture.CaptureBackend.WindowsGraphicsCapture)throw new ArgumentOutOfRangeException(nameof(resolvedSource));
        if(!Filters.Contains("gfxcapture"))return ["gfxcapture"];
        var missing=new List<string>();
        if(!Inputs.Contains("lavfi"))missing.Add("lavfi");
        foreach(var filter in new[]{"gfxcapture","hwdownload","format","setpts","fps"}.Concat(composeMonitors?new[]{"color","overlay"}:[]))
            if(!Filters.Contains(filter))missing.Add(filter);
        foreach(var option in new[]{"hmonitor","capture_cursor","max_framerate","crop_left","crop_top","crop_right","crop_bottom","width","height","resize_mode","output_fmt"})
            if(!GfxCaptureOptions.Contains(option))missing.Add("gfxcapture."+option);
        if(GfxCaptureProbeError is not null)missing.Add(Loc.T("gfxcapture help query failed"));
        return missing;
    }
    public IReadOnlyList<string> Missing(RecordingOptions options, bool withAudio)
    {
        var missing = new List<string>();
        if (!Inputs.Contains("gdigrab")) missing.Add(Loc.T("gdigrab input"));
        if (!Encoders.Contains(options.EncoderName)) missing.Add(options.EncoderName + Loc.T(" encoder"));
        if (withAudio && !Encoders.Contains(options.AudioEncoder == AudioEncoder.Aac ? "aac" : "libopus")) missing.Add(Loc.T("selected audio encoder"));
        if (withAudio && options.AudioSource == AudioSource.DirectShow && !Inputs.Contains("dshow")) missing.Add(Loc.T("dshow input"));
        foreach (var filter in new[] { "pad", "format" }.Concat(options.ScaleMode == ScaleMode.Original ? [] : new[] { "scale" }))
            if (!Filters.Contains(filter)) missing.Add(filter + Loc.T(" filter"));
        return missing;
    }
}

public sealed record FfmpegProbeResult(string Path, FfmpegCapabilities Capabilities);

public sealed class FfmpegProbe(ICommandRunner runner)
{
    public async Task<FfmpegProbeResult> TestAsync(string path, CancellationToken token = default)
    {
        async Task<string> Query(params string[] options)
        {
            token.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(path, ["-hide_banner", ..options], token);
            if (result.ExitCode != 0) throw new IOException(Loc.F("FFmpeg {0} failed (exit {1}): {2}",string.Join(" ",options),result.ExitCode,result.StandardError[..Math.Min(result.StandardError.Length,1000)]));
            return result.StandardOutput + "\n" + result.StandardError;
        }
        var versionOutput = await Query("-version");
        var version = versionOutput.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("ffmpeg version ", StringComparison.Ordinal));
        if (version is null) throw new InvalidDataException(Loc.T("The selected executable did not identify itself as FFmpeg."));
        var devices = await Query("-devices");
        var encoders = await Query("-encoders");
        var filters = await Query("-filters");
        static HashSet<string> Names(string text, string expression) => Regex.Matches(text, expression,
            RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200))
            .SelectMany(match => match.Groups[1].Value.Split(',')).ToHashSet(StringComparer.Ordinal);
        var capabilities=new FfmpegCapabilities(version,
            Names(devices, @"^\s*D[E ]\s+(\S+)\s"),
            Names(encoders, @"^\s*[VAS][A-Z.]{5}\s+(\S+)\s"),
            // FFmpeg uses two capability flags in newer builds and three in older builds.
            // Require a real filter name and input/output signature so legend rows are ignored.
            Names(filters, @"^[ \t]*[A-Z.]{2,}[ \t]+([a-zA-Z0-9_]+)[ \t]+[AVN|]+->[AVN|]+(?:[ \t]|$)"));
        if(capabilities.Filters.Contains("gfxcapture"))
        {
            try
            {
                var help=await Query("-h","filter=gfxcapture");
                capabilities=capabilities with{GfxCaptureOptions=Names(help,@"^[ \t]+([a-zA-Z0-9_]+)[ \t]+<[^>]+>[ \t]+")};
            }
            catch(Exception error)when(error is IOException or TimeoutException)
            {capabilities=capabilities with{GfxCaptureProbeError=error.Message};}
        }
        token.ThrowIfCancellationRequested();
        return new(path,capabilities);
    }
}
