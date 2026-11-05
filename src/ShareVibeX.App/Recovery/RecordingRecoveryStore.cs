using System.Text.Json;
using System.Text.RegularExpressions;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Recovery;

public sealed record RecoverableRecording(string Directory,RecordingWorkspace? Workspace,RecordingOptions? Options,PixelRect Region,PreviewMedia? Media,RecordingSegment[] Segments,string? Error);
public sealed class RecordingRecoveryStore(string root,string clipboardRoot)
{
    private sealed record Manifest(int Schema,string? State,PixelRect Region,RecordingOptions? Options,string[]? Owned,RecordingResult? Result,RecordingSegment[]? Segments,bool? HasAudio);
    public IReadOnlyList<RecoverableRecording> List()
    {
        if(!System.IO.Directory.Exists(root))return [];RejectLink(root);var entries=new List<RecoverableRecording>();
        foreach(var directory in System.IO.Directory.EnumerateDirectories(root).Take(1000))
        {
            if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out _))continue;
            try{entries.Add(Read(directory));}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OverflowException)
            {entries.Add(new(directory,null,null,default,null,[],Loc.T("Retained for manual recovery: ")+error.Message));}
        }
        return entries;
    }
    public RecoverableRecording Read(string directory)
    {
        directory=Path.GetFullPath(directory);if(!string.Equals(Path.GetDirectoryName(directory),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(directory),"N",out _))throw new IOException(Loc.T("Session escaped the recording folder."));
        RejectLink(root);RejectLink(directory);var path=Path.Combine(directory,"manifest.json");RejectLink(path);
        if(new FileInfo(path).Length>1024*1024)throw new IOException(Loc.T("Recovery manifest is too large."));
        var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path),new JsonSerializerOptions{MaxDepth=32})??throw new IOException(Loc.T("Empty recovery manifest."));
        if(manifest.Schema!=2||manifest.Options is null||manifest.Owned is null||manifest.Owned.Length>10000)throw new IOException(Loc.T("Unsupported recovery manifest."));
        var options=manifest.Options;RecordingValidation.Validate(options);var size=VideoScaling.Calculate(manifest.Region,options);_ = manifest.Region.Right;_ = manifest.Region.Bottom;
        var workspace=RecordingWorkspace.OpenExisting(root,directory,options,manifest.Region,manifest.Owned);
        var segments=manifest.Segments??[];if(segments.Length>10000)throw new IOException(Loc.T("Too many recording segments."));
        foreach(var segment in segments)
        {
            if(segment is null||string.IsNullOrEmpty(segment.Path))throw new IOException(Loc.T("Invalid retained segment metadata."));
            var name=Path.GetFileName(segment.Path);if(!RecordingWorkspace.IsOwnedName(name)||!workspace.Contains(segment.Path)||segment.Duration<TimeSpan.Zero||segment.TotalBytes<0||segment.Width is <1 or >16384||segment.Height is <1 or >16384)throw new IOException(Loc.T("Invalid retained segment metadata."));
            if(File.Exists(segment.Path))RejectLink(segment.Path);
        }
        PreviewMedia? media=null;string? warning=Loc.T("Interrupted recording. Open the folder for manual recovery; media was not repaired.");
        var result=manifest.Result;
        if(result?.Success==true&&result.OutputPath is {} output)
        {
            if(!string.Equals(Path.GetFullPath(output),workspace.CapturePath,StringComparison.OrdinalIgnoreCase))throw new IOException(Loc.T("Completed media escaped its session."));
            RejectLink(output);if(new FileInfo(output).Length==0)throw new IOException(Loc.T("Retained recording is empty."));
            if(manifest.HasAudio is {} audio)
            {
                TimeSpan? duration=segments.Length==0||segments.Any(s=>s.Duration is null)?null:TimeSpan.FromTicks(segments.Sum(s=>s.Duration!.Value.Ticks));
                media=new(output,options.FfmpegPath,duration,size.Width,size.Height,options.FrameRate,audio);warning=null;
            }
            else warning=Loc.T("Legacy recording metadata has no final audio flag. Open the folder for manual recovery.");
        }
        return new(directory,workspace,options,manifest.Region,media,segments,warning);
    }
    public Task DiscardAsync(RecoverableRecording recording)
    {
        if(recording.Workspace is null)return Task.FromException(new IOException(Loc.T("This manifest cannot authorize cleanup. Inspect its folder manually.")));
        return Read(recording.Directory).Workspace!.DiscardAsync();
    }
    public Task ClearClipboardAsync()=>Task.Run(()=>
    {
        if(!System.IO.Directory.Exists(clipboardRoot))return;RejectLink(clipboardRoot);
        var files=System.IO.Directory.EnumerateFiles(clipboardRoot).Where(p=>Regex.IsMatch(Path.GetFileName(p),@"\AShareVibeX-clipboard-[a-f0-9]{32}\.(mp4|mkv|webm|gif|webp|apng)(?:\.partial\.\1)?\z",RegexOptions.CultureInvariant)).ToArray();
        foreach(var file in files)RejectLink(file);
        foreach(var file in files)File.Delete(file);
    });
    private static void RejectLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException(Loc.T("Linked recovery paths are not supported."));}
}
