using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Recording;

public interface IRecordingAssembler
{
    Task<RecordingResult> AssembleAsync(RecordingOptions options,RecordingWorkspace workspace,IReadOnlyList<RecordingSegment> segments,bool keepAudio,CancellationToken token);
}
public sealed class RecordingAssembler(IRecordingProcessFactory processes,Func<string?,string>? resolve=null):IRecordingAssembler
{
    public async Task<RecordingResult> AssembleAsync(RecordingOptions options,RecordingWorkspace workspace,IReadOnlyList<RecordingSegment> segments,bool keepAudio,CancellationToken token)
    {
        IRecordingProcess? process=null;
        try
        {
            token.ThrowIfCancellationRequested();
            if(!options.IsAnimation && segments.Count==1 && keepAudio==segments[0].HasAudio)
            {
                await using(var input=File.OpenRead(segments[0].Path))
                await using(var output=new FileStream(workspace.WorkingPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
                    await input.CopyToAsync(output,token);
            }
            else
            {
                var args=RecordingAssemblyCommand.Build(options,segments,keepAudio,workspace.WorkingPath);
                if(args.Sum(a=>a.Length+3)>28000)throw new IOException(Loc.T("Segment assembly command is too large. Original segments were retained."));
                var executable=(resolve??(path=>FfmpegLocator.Resolve(path,AppContext.BaseDirectory)))(options.FfmpegPath);
                process=processes.Start(executable,new[]{"-progress","pipe:1","-nostats","-loglevel","warning"}.Concat(args).ToArray());
                var exit=await process.Exit.WaitAsync(token);
                if(exit!=0)throw new IOException(Loc.T("FFmpeg assembly failed: ")+process.Diagnostics);
            }
            token.ThrowIfCancellationRequested();
            if(!File.Exists(workspace.WorkingPath)||new FileInfo(workspace.WorkingPath).Length==0)throw new IOException(Loc.T("Assembly produced no usable output."));
            File.Move(workspace.WorkingPath,workspace.CapturePath);return new(true,workspace.CapturePath,null,null);
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception error){return new(false,null,File.Exists(workspace.WorkingPath)?workspace.WorkingPath:segments.FirstOrDefault()?.Path,error.Message);}
        finally
        {
            if(process is not null)
            {
                try
                {
                    if(!process.Exit.IsCompleted){process.Kill();try{await process.Exit.WaitAsync(TimeSpan.FromSeconds(3));}catch{}}
                }
                finally{await process.DisposeAsync();}
            }
        }
    }
}
