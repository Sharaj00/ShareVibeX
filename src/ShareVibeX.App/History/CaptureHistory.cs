using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.History;

public sealed record CaptureHistoryContext(string Application,DateTime CapturedAt);

public sealed class CaptureHistory(Func<AppSettings> settings,Func<string,FileAttributes>? readAttributes=null)
{
    private static readonly HashSet<string> MediaExtensions=new(StringComparer.OrdinalIgnoreCase)
        {".png",".jpg",".jpeg",".webp",".mp4",".mkv",".webm",".gif",".apng"};
    public CaptureHistoryContext Current {get;set;}=new("Desktop",DateTime.Now);
    public string Root=>Path.GetFullPath(settings().CaptureDirectory);
    public string FolderFor(DateTime date)=>Path.GetFullPath(settings().CaptureDirectoryFor(date));
    public static string FileName(CaptureHistoryContext context,string extension)
    {
        var invalid=Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var name=new string(context.Application.Select(c=>invalid.Contains(c)||char.IsControl(c)?'_':c).Take(100).ToArray()).Trim().TrimEnd('.');
        if(string.IsNullOrWhiteSpace(name))name="Desktop";
        return FormattableString.Invariant($"{name}_{context.CapturedAt:yyyyMMdd_HHmmss}.{extension.TrimStart('.')}");
    }
    public async Task<string?> StoreAsync(CaptureHistoryContext context,string extension,Func<string,CancellationToken,Task> write,CancellationToken token=default)
    {
        if(!settings().HistoryEnabled)return null;
        if(!MediaExtensions.Contains("."+extension))throw new ArgumentException(Loc.T("Unsupported history format."));
        token.ThrowIfCancellationRequested();
        var folder=FolderFor(context.CapturedAt);
        Directory.CreateDirectory(folder);
        var filename=FileName(context,extension);
        var stem=Path.GetFileNameWithoutExtension(filename);
        var temporary=Path.Combine(folder,".ShareVibeX-"+Guid.NewGuid().ToString("N")+".partial."+extension);
        try
        {
            await write(temporary,token);
            for(var number=1;;number++)
            {
                token.ThrowIfCancellationRequested();
                var path=Path.Combine(folder,number==1?filename:$"{stem}_{number}.{extension}");
                if(File.Exists(path))continue;
                try{File.Move(temporary,path,false);return path;}
                catch(IOException)when(File.Exists(path)){ }
            }
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public Task<string?> StoreFileAsync(CaptureHistoryContext context,string source,CancellationToken token=default)
    {
        if(!settings().HistoryEnabled)return Task.FromResult<string?>(null);
        token.ThrowIfCancellationRequested();
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(MediaExtensions.Contains(Path.GetExtension(source))&&File.Exists(source)&&string.Equals(Path.GetDirectoryName(Path.GetFullPath(source)),FolderFor(context.CapturedAt),comparison))
            return Task.FromResult<string?>(source);
        return StoreAsync(context,Path.GetExtension(source).TrimStart('.').ToLowerInvariant(),async(path,ct)=>
    {
        await using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        await input.CopyToAsync(output,ct);
    },token);
    }
    public IReadOnlyList<string> List()=>ListEntries().Select(e=>e.Path).ToArray();
    public Task<IReadOnlyList<CaptureHistoryEntry>> ListEntriesAsync(CancellationToken token=default)
    {var root=Root;return Task.Run(()=>ReadEntries(root,token,readAttributes??File.GetAttributes),token);}
    public IReadOnlyList<CaptureHistoryEntry> ListEntries()=>ReadEntries(Root,default,readAttributes??File.GetAttributes);
    private static IReadOnlyList<CaptureHistoryEntry> ReadEntries(string root,CancellationToken token,Func<string,FileAttributes> attributes)
    {
        if(!Directory.Exists(root))return [];
        var entries=new List<CaptureHistoryEntry>();
        foreach(var folder in Directory.EnumerateDirectories(root).Prepend(root))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if(folder!=root&&(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(folder),@"\A\d{4}-\d{2}\z")||(attributes(folder)&FileAttributes.ReparsePoint)!=0))continue;
                foreach(var path in Directory.EnumerateFiles(folder))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if(Path.GetFileName(path).StartsWith('.')||!MediaExtensions.Contains(Path.GetExtension(path))||(attributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                        entries.Add(new(path,CaptureHistoryEntry.CaptureTime(path)));
                    }
                    catch(Exception error)when(IsUnavailable(error)){ /* A removed/unreadable item must not hide other captures. */ }
                }
            }
            catch(Exception error)when(IsUnavailable(error)){ /* Keep entries from other months if this month disappears. */ }
        }
        token.ThrowIfCancellationRequested();
        return entries.OrderByDescending(e=>e.CapturedAt).ThenByDescending(e=>e.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static bool IsUnavailable(Exception error)=>error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}

public sealed record CaptureHistoryEntry(string Path,DateTime CapturedAt)
{
    public string Month=>CapturedAt.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture);
    public static DateTime CaptureTime(string path)
    {
        var match=System.Text.RegularExpressions.Regex.Match(System.IO.Path.GetFileNameWithoutExtension(path),@"_(\d{8})_(\d{6})(?:_\d+)?$");
        if(match.Success&&DateTime.TryParseExact(match.Groups[1].Value+match.Groups[2].Value,"yyyyMMddHHmmss",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date))return date;
        return File.GetCreationTime(path);
    }
}
