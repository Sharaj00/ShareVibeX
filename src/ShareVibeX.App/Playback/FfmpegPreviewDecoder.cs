using System.Diagnostics;
using System.Text;
using ShareVibeX.Core.Playback;
using ShareVibeX.App.Recording;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Playback;

public sealed class FfmpegPreviewDecoderFactory(Func<string?,string>? resolve=null):IPreviewDecoderFactory
{
    public async Task<IPreviewDecoder> OpenAsync(PreviewMedia media,PreviewFormat format,TimeSpan position,bool audio,CancellationToken token)
    {
        var opening=Task.Run<IPreviewDecoder>(()=>
        {
            var executable=(resolve??(value=>FfmpegLocator.Resolve(value,AppContext.BaseDirectory)))(media.FfmpegPath);
            var video=new RawPreviewProcess(executable,PreviewDecoderCommand.Video(media,format,position));
            try{return new Decoder(video,audio&&media.HasAudio?new RawPreviewProcess(executable,PreviewDecoderCommand.Audio(media,position)):null);}
            catch{video.DisposeAsync().AsTask().GetAwaiter().GetResult();throw;}
        },token);
        IPreviewDecoder? decoder=null;
        try{decoder=await opening.ConfigureAwait(false);token.ThrowIfCancellationRequested();return decoder;}
        catch{if(decoder is not null)await decoder.DisposeAsync().ConfigureAwait(false);throw;}
    }
    private sealed class Decoder(RawPreviewProcess video,RawPreviewProcess? audio):IPreviewDecoder
    {
        public Stream Video=>video.Output;public Stream? Audio=>audio?.Output;
        public Task VideoCompletion=>video.Exit;public Task AudioCompletion=>audio?.Exit??Task.CompletedTask;
        public async ValueTask DisposeAsync(){try{await video.DisposeAsync().ConfigureAwait(false);}finally{if(audio is not null)await audio.DisposeAsync().ConfigureAwait(false);}}
    }
}

internal sealed class RawPreviewProcess:IAsyncDisposable
{
    private readonly Process process;
    private readonly WindowsChildProcessJob? job;
    private readonly StringBuilder error=new();
    private readonly Task stderr;
    private int disposed;
    public Stream Output=>process.StandardOutput.BaseStream;
    public Task Exit {get;}
    public RawPreviewProcess(string executable,IReadOnlyList<string> args)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)info.ArgumentList.Add(arg);process=new(){StartInfo=info};
        try{if(OperatingSystem.IsWindows())job=new();if(!process.Start())throw new IOException(Loc.T("Could not start the video decoder."));job?.Assign(process.Handle);}
        catch{try{if(process.Id!=0&&!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}finally{try{job?.Dispose();}finally{process.Dispose();}}throw;}
        stderr=ReadErrorAsync();Exit=ObserveAsync();
    }
    private async Task ReadErrorAsync()
    {
        var buffer=new char[1024];int count;
        while((count=await process.StandardError.ReadAsync(buffer).ConfigureAwait(false))>0)
        {error.Append(buffer,0,count);if(error.Length>8192)error.Remove(0,error.Length-8192);}
    }
    private async Task ObserveAsync()
    {
        await process.WaitForExitAsync().ConfigureAwait(false);await stderr.ConfigureAwait(false);
        if(process.ExitCode!=0)throw new IOException(Loc.T("Video decoder failed: ")+error);
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        try{if(!process.HasExited)process.Kill(true);try{await Exit.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);}catch(IOException){} }
        finally{try{job?.Dispose();}finally{process.Dispose();}}
    }
}
