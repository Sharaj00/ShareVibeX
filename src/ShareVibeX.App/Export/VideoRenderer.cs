using ShareVibeX.Core.Playback;
using ShareVibeX.Core.Editing;
using ShareVibeX.Core.Recording;
using ShareVibeX.App.Recording;

namespace ShareVibeX.App.Export;

public interface IVideoRenderer
{
    Task RenderAsync(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output,Action<RecordingProgress>? progress,CancellationToken token);
}
public sealed class VideoRenderer(IRecordingProcessFactory processes,Func<string?,string>? resolve=null):IVideoRenderer
{
    public async Task RenderAsync(PreviewMedia source,VideoEditTimeline edit,RecordingOptions options,string output,Action<RecordingProgress>? progress,CancellationToken token)
    {
        RecordingValidation.Validate(options);
        token.ThrowIfCancellationRequested();if(File.Exists(output))throw new IOException(Loc.T("The temporary export path already exists."));
        if(edit.IsOriginal&&string.Equals(Path.GetExtension(source.Path),"."+options.Extension,StringComparison.OrdinalIgnoreCase))
        {
            await using(var input=File.OpenRead(source.Path))
            await using(var target=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))await input.CopyToAsync(target,token);
        }
        else
        {
            if(source.Duration is null)throw new InvalidDataException(Loc.T("Duration is unavailable. Save this recording in its original container."));
            var args=VideoEditCommand.Build(source,edit,options,output);if(args.Sum(s=>s.Length+3)>28000)throw new IOException(Loc.T("Too many edit intervals for one render. Original media was retained."));
            var executable=(resolve??(path=>FfmpegLocator.Resolve(path,AppContext.BaseDirectory)))(source.FfmpegPath);
            var process=processes.Start(executable,new[]{"-progress","pipe:1","-nostats","-loglevel","warning"}.Concat(args).ToArray());
            try
            {
                while(!process.Exit.IsCompleted){await Task.WhenAny(process.Exit,Task.Delay(200,token));token.ThrowIfCancellationRequested();progress?.Invoke(process.Progress);}
                if(await process.Exit!=0)throw new IOException(Loc.T("Video export failed: ")+process.Diagnostics);
            }
            finally
            {
                try{if(!process.Exit.IsCompleted){process.Kill();try{await process.Exit.WaitAsync(TimeSpan.FromSeconds(3));}catch{}}}
                finally{await process.DisposeAsync();}
            }
        }
        token.ThrowIfCancellationRequested();if(!File.Exists(output)||new FileInfo(output).Length==0)throw new IOException(Loc.T("Export produced no usable video file."));
    }
}
