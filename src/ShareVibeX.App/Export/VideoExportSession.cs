using System.Text.Json;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Export;

public sealed record VideoEditSnapshot(int Schema,long OriginalFrames,int FrameRate,VideoFrameSpan[] Spans,string? ExportedFingerprint,string CurrentFile);
public sealed class VideoExportSession:IAsyncDisposable
{
    public IReadOnlyList<VideoContainer> SupportedContainers=>RecordingValidation.Containers(options.Encoder);
    private readonly PreviewMedia source;private readonly RecordingOptions options;private readonly RecordingWorkspace workspace;
    private readonly string cacheRoot;private readonly IVideoRenderer renderer;
    private readonly Dictionary<string,string> materialized=[],copies=[];
    private readonly object gate=new();private CancellationTokenSource? operation;private Task active=Task.CompletedTask;private bool disposed,busy;
    private string? exported;
    public VideoEditTimeline Timeline {get;private set;}
    public PreviewMedia CurrentMedia=>source with{Path=materialized[Timeline.Fingerprint],Duration=CanEdit?Timeline.Duration:source.Duration};
    public bool CanEdit=>source.Duration>TimeSpan.Zero;
    public bool HasUnexportedChanges=>exported!=Timeline.Fingerprint;
    public bool IsBusy{get{lock(gate)return busy;}}
    public double? Progress {get;private set;}
    public string? Warning {get;private set;}
    public VideoExportSession(PreviewMedia source,RecordingOptions options,RecordingWorkspace workspace,string cacheRoot,IVideoRenderer renderer)
    {
        this.source=source;this.options=options;this.workspace=workspace;this.cacheRoot=Path.GetFullPath(cacheRoot);this.renderer=renderer;
        Timeline=new(Math.Max(1,(long)Math.Round((source.Duration?.TotalSeconds??0)*source.FrameRate)),source.FrameRate);materialized[Timeline.Fingerprint]=source.Path;
    }
    public Task ApplyAsync(long a,long b,bool keep,CancellationToken token=default)=>BeginAsync(async ct=>
    {
        if(!CanEdit)throw new InvalidDataException(Loc.T("A–B editing requires known recording duration."));
        var candidate=keep?Timeline.Keep(a,b):Timeline.Remove(a,b);
        if(!materialized.ContainsKey(candidate.Fingerprint))
        {
            var path=workspace.CreateDerivedFile(options.Extension);
            try{await renderer.RenderAsync(source,candidate,options,path,p=>Report(p,candidate),ct);ct.ThrowIfCancellationRequested();materialized.Add(candidate.Fingerprint,path);}
            catch{TryDelete(path);throw;}
        }
        Timeline=candidate;await PersistAsync(CancellationToken.None);
    },token);
    public Task ResetAsync(CancellationToken token=default)=>BeginAsync(async ct=>{ct.ThrowIfCancellationRequested();Timeline=Timeline.Reset();await PersistAsync(CancellationToken.None);},token);
    internal Task SelectRevisionAsync(VideoEditTimeline revision)=>BeginAsync(async _=>
    {
        if(revision.OriginalFrames!=Timeline.OriginalFrames||revision.FrameRate!=Timeline.FrameRate||!materialized.ContainsKey(revision.Fingerprint))throw new ArgumentException(Loc.T("The previous preview revision is unavailable."));
        Timeline=revision;await PersistAsync(CancellationToken.None);
    },CancellationToken.None);
    public Task CopyAsync(Func<string,CancellationToken,Task> publish,CancellationToken token=default)=>BeginAsync(async ct=>
    {
        var key=Timeline.Fingerprint;
        if(!copies.TryGetValue(key,out var path)||!File.Exists(path))
        {
            Directory.CreateDirectory(cacheRoot);path=Path.Combine(cacheRoot,"ShareVibeX-clipboard-"+Guid.NewGuid().ToString("N")+"."+options.Extension);
            var temporary=path+".partial."+options.Extension;
            try{await renderer.RenderAsync(source,Timeline,options,temporary,p=>Report(p,Timeline),ct);ct.ThrowIfCancellationRequested();File.Move(temporary,path);copies[key]=path;}
            finally{TryDelete(temporary);}
        }
        await publish(path,ct);exported=key;await PersistAsync(CancellationToken.None);
    },token);
    public Task SaveAsync(string destination,CancellationToken token=default)=>BeginAsync(async ct=>
    {
        destination=Path.GetFullPath(destination);
        if(workspace.Contains(destination)||IsWithin(destination,cacheRoot))throw new ArgumentException(Loc.T("Choose a destination outside the recording workspace and clipboard cache."));
        var extension=Path.GetExtension(destination).ToLowerInvariant();var outputOptions=options with{Container=extension switch{".mp4"=>VideoContainer.Mp4,".mkv"=>VideoContainer.Matroska,".webm"=>VideoContainer.Webm,".gif"=>VideoContainer.Gif,".webp"=>VideoContainer.AnimatedWebp,".apng"=>VideoContainer.Apng,_=>throw new ArgumentException(Loc.T("Choose a supported video or animation file."))}};
        if(outputOptions.Container==VideoContainer.Webm)outputOptions=outputOptions with{AudioEncoder=AudioEncoder.Libopus,AudioSampleRate=48000};
        RecordingValidation.Validate(outputOptions);
        var parent=Path.GetDirectoryName(destination)!;Directory.CreateDirectory(parent);var temporary=Path.Combine(parent,".ShareVibeX-"+Guid.NewGuid().ToString("N")+".partial"+extension);
        try{await renderer.RenderAsync(source,Timeline,outputOptions,temporary,p=>Report(p,Timeline),ct);ct.ThrowIfCancellationRequested();File.Move(temporary,destination,true);exported=Timeline.Fingerprint;await PersistAsync(CancellationToken.None);}
        finally{TryDelete(temporary);}
    },token);
    private void Report(RecordingProgress value,VideoEditTimeline edit)=>Progress=value.Duration is {} time?Math.Clamp(time.TotalSeconds/edit.Duration.TotalSeconds,0,1):null;
    public Task RestoreAsync(CancellationToken token=default)=>BeginAsync(async ct=>
    {
        if(!File.Exists(workspace.EditManifestPath)||!CanEdit)return;
        try
        {
            if(new FileInfo(workspace.EditManifestPath).Length>1024*1024)throw new IOException(Loc.T("Edit metadata is too large."));
            var snapshot=JsonSerializer.Deserialize<VideoEditSnapshot>(await File.ReadAllTextAsync(workspace.EditManifestPath,ct),new JsonSerializerOptions{MaxDepth=16})??throw new IOException(Loc.T("Empty edit metadata."));
            if(snapshot.Schema!=1||snapshot.OriginalFrames!=Timeline.OriginalFrames||snapshot.FrameRate!=Timeline.FrameRate||snapshot.Spans is null||snapshot.Spans.Length>1000)throw new IOException(Loc.T("Edit metadata does not match this recording."));
            var edit=new VideoEditTimeline(snapshot.OriginalFrames,snapshot.FrameRate,snapshot.Spans);
            if(!edit.IsOriginal)
            {
                if(string.IsNullOrEmpty(snapshot.CurrentFile)||!System.Text.RegularExpressions.Regex.IsMatch(snapshot.CurrentFile,@"\Aedit-[a-f0-9]{32}\.(mp4|mkv|webm|gif|webp|apng)\z"))throw new IOException(Loc.T("Invalid edited media path."));
                var path=Path.Combine(workspace.DirectoryPath,snapshot.CurrentFile);
                if(File.Exists(path))
                {
                    if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length==0)throw new IOException(Loc.T("Invalid edited media file."));
                }
                else
                {
                    path=workspace.CreateDerivedFile(options.Extension);
                    try{await renderer.RenderAsync(source,edit,options,path,p=>Report(p,edit),ct);ct.ThrowIfCancellationRequested();}catch{TryDelete(path);throw;}
                }
                materialized[edit.Fingerprint]=path;
            }
            Timeline=edit;exported=snapshot.ExportedFingerprint;
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OverflowException)
        {Warning=Loc.T("Edits could not be restored; the original recording is available. ")+error.Message;}
    },token);
    private Task BeginAsync(Func<CancellationToken,Task> body,CancellationToken token)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);if(busy)throw new InvalidOperationException(Loc.T("Wait for the current video action to finish."));
            busy=true;Progress=null;operation=CancellationTokenSource.CreateLinkedTokenSource(token);active=RunAsync(body,operation);return active;
        }
    }
    private async Task RunAsync(Func<CancellationToken,Task> body,CancellationTokenSource cancellation)
    {
        await Task.Yield();
        try{await body(cancellation.Token);}
        finally{lock(gate){busy=false;operation=null;}cancellation.Dispose();}
    }
    public async Task CancelAsync()
    {
        Task pending;lock(gate){operation?.Cancel();pending=active;}try{await pending;}catch(OperationCanceledException){}catch{ /* The initiating action reports its error. */ }
    }
    private async Task PersistAsync(CancellationToken token)
    {
        try{await workspace.WriteEditAsync(JsonSerializer.Serialize(new VideoEditSnapshot(1,Timeline.OriginalFrames,Timeline.FrameRate,Timeline.Spans.ToArray(),exported,Path.GetFileName(CurrentMedia.Path))),token);}
        catch(OperationCanceledException){throw;}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){Warning=Loc.T("Recovery metadata could not be saved: ")+error.Message;}
    }
    public static bool IsWithin(string path,string directory)=>Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static void TryDelete(string path){try{if(File.Exists(path))File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
    public async ValueTask DisposeAsync(){lock(gate)disposed=true;await CancelAsync();}
}
