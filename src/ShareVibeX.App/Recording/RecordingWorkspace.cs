using System.Text.Json;
using System.Text.RegularExpressions;
using ShareVibeX.Core.Capture;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Recording;

public sealed class RecordingWorkspace
{
    public string DirectoryPath {get;}
    public string CapturePath {get;}
    public string WorkingPath {get;}
    public string ManifestPath=>Path.Combine(DirectoryPath,"manifest.json");
    public string EditManifestPath=>Path.Combine(DirectoryPath,"edit.json");
    private string NextManifest=>Path.Combine(DirectoryPath,"manifest.next");
    private readonly string root;
    private readonly RecordingOptions options;
    private readonly PixelRect region;
    private readonly List<string> owned=[];
    private int segmentIndex;
    private RecordingWorkspace(string root,string directory,RecordingOptions options,PixelRect region)
    {
        this.root=root;DirectoryPath=directory;this.options=options;this.region=region;
        CapturePath=Path.Combine(directory,"capture."+options.Extension);
        WorkingPath=Path.Combine(directory,"capture.partial."+options.Extension);
        owned.AddRange([CapturePath,WorkingPath,ManifestPath,NextManifest,EditManifestPath,Path.Combine(directory,"edit.next")]);
    }
    public string CreateDerivedFile(string extension)
    {
        Guard();if(extension is not("mp4" or "mkv" or "webm" or "gif" or "webp" or "apng"))throw new ArgumentException(Loc.T("Unsupported video container."));
        var path=Path.Combine(DirectoryPath,"edit-"+Guid.NewGuid().ToString("N")+"."+extension);owned.Add(path);return path;
    }
    public bool Contains(string path)=>Path.GetFullPath(path).StartsWith(DirectoryPath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    public async Task WriteEditAsync(string json,CancellationToken token)
    {
        Guard();var next=Path.Combine(DirectoryPath,"edit.next");await File.WriteAllTextAsync(next,json,token);File.Move(next,EditManifestPath,true);
    }
    public (string Capture,string Working) CreateSegment(string? extension=null)
    {
        extension??=options.Extension;
        if(extension is not ("mp4" or "mkv" or "webm" or "gif" or "webp" or "apng"))throw new ArgumentException(Loc.T("Unsupported video container."));
        Guard();var name="segment-"+(++segmentIndex).ToString("D6",System.Globalization.CultureInfo.InvariantCulture);
        var capture=Path.Combine(DirectoryPath,name+"."+extension);var working=Path.Combine(DirectoryPath,name+".partial."+extension);
        owned.Add(capture);owned.Add(working);return(capture,working);
    }
    public static RecordingWorkspace Create(string root,RecordingOptions options,PixelRect region)
    {
        root=Path.GetFullPath(root);Directory.CreateDirectory(root);RejectLink(root);
        var directory=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        return new(root,directory,options,region);
    }
    public async Task WriteManifestAsync(string state,RecordingResult? result=null,CancellationToken token=default,IReadOnlyList<RecordingSegment>? segments=null,bool? hasAudio=null,PixelRect? currentRegion=null)
    {
        Guard();
        var document=new {Schema=2,State=state,Region=currentRegion??region,Options=options,Capture=Path.GetFileName(CapturePath),Working=Path.GetFileName(WorkingPath),Result=result,Owned=owned.Select(Path.GetFileName).ToArray(),Segments=segments,HasAudio=hasAudio};
        await File.WriteAllTextAsync(NextManifest,JsonSerializer.Serialize(document),token).ConfigureAwait(false);
        File.Move(NextManifest,ManifestPath,true);
    }
    public Task DiscardAsync()=>Task.Run(()=>
    {
        Guard();
        // Cleanup trusts only immutable paths created by this instance, never manifest contents.
        foreach(var path in owned)
        {
            if(!File.Exists(path))continue;RejectLink(path);File.Delete(path);
        }
        if(!Directory.EnumerateFileSystemEntries(DirectoryPath).Any())Directory.Delete(DirectoryPath);
    });
    private void Guard()
    {
        if(!string.Equals(Path.GetDirectoryName(DirectoryPath),root,StringComparison.OrdinalIgnoreCase))throw new IOException(Loc.T("Session path escaped the recording workspace."));
        RejectLink(root);RejectLink(DirectoryPath);
        foreach(var path in owned)if(File.Exists(path))RejectLink(path);
    }
    public static bool IsOwnedName(string name)=>Regex.IsMatch(name,@"\A(?:capture(?:\.partial)?\.(?:mp4|mkv|webm|gif|webp|apng)|segment-[0-9]{6}(?:\.partial)?\.(?:mp4|mkv|webm|gif|webp|apng)|edit-[a-f0-9]{32}\.(?:mp4|mkv|webm|gif|webp|apng)|manifest\.(?:json|next)|edit\.(?:json|next))\z",RegexOptions.CultureInvariant);
    public static RecordingWorkspace OpenExisting(string root,string directory,RecordingOptions options,PixelRect region,IEnumerable<string> names)
    {
        root=Path.GetFullPath(root);directory=Path.GetFullPath(directory);
        if(Path.GetDirectoryName(directory)!=root||!Guid.TryParseExact(Path.GetFileName(directory),"N",out _))throw new IOException(Loc.T("Invalid recording session path."));
        var workspace=new RecordingWorkspace(root,directory,options,region);
        foreach(var name in names){if(!IsOwnedName(name))throw new IOException(Loc.T("Unrecognized recording file name."));workspace.owned.Add(Path.Combine(directory,name));}
        foreach(var path in Directory.EnumerateFiles(directory))if(IsOwnedName(Path.GetFileName(path)))workspace.owned.Add(path);
        workspace.Guard();return workspace;
    }
    private static void RejectLink(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException(Loc.T("Recording workspace links are not supported."));
    }
}
