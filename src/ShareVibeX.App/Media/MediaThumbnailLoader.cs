using System.Diagnostics;
using Avalonia.Media.Imaging;
using ShareVibeX.App.Recording;

namespace ShareVibeX.App.Media;

public static class MediaThumbnailLoader
{
    private static readonly SemaphoreSlim Workers=new(2,2);
    public static async Task<Bitmap?> LoadAsync(string path,string? ffmpegPath=null,int width=240,CancellationToken token=default)
    {
        await Workers.WaitAsync(token).ConfigureAwait(false);
        try
        {
            width=Math.Clamp(width,64,640);
            if(Path.GetExtension(path).ToLowerInvariant() is not (".mp4" or ".mkv" or ".webm" or ".apng"))
                return await Task.Run(()=>
                {
                    token.ThrowIfCancellationRequested();using var codec=SkiaSharp.SKCodec.Create(path);if(codec is null)return null;
                    using var stream=File.OpenRead(path);
                    var bitmap=codec.Info.Height>codec.Info.Width?Bitmap.DecodeToHeight(stream,width):Bitmap.DecodeToWidth(stream,width);
                    if(token.IsCancellationRequested){bitmap.Dispose();token.ThrowIfCancellationRequested();}return bitmap;
                },token).ConfigureAwait(false);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var executable=FfmpegLocator.Resolve(ffmpegPath,ShareVibeX.Core.IO.PortablePaths.BaseDirectory);
            var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var argument in new[]{"-v","error","-nostdin","-i",path,"-an","-frames:v","1","-vf",$"scale={width}:{width}:force_original_aspect_ratio=decrease","-f","image2pipe","-vcodec","png","pipe:1"})info.ArgumentList.Add(argument);
            using var process=new Process{StartInfo=info};process.Start();
            using var registration=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}});
            var errors=process.StandardError.BaseStream.CopyToAsync(Stream.Null,timeout.Token);
            try
            {
            using var bytes=new MemoryStream();var buffer=new byte[8192];int read;
            while((read=await process.StandardOutput.BaseStream.ReadAsync(buffer,timeout.Token).ConfigureAwait(false))>0)
            {if(bytes.Length+read>8*1024*1024)throw new IOException("Thumbnail exceeds limit.");await bytes.WriteAsync(buffer.AsMemory(0,read),timeout.Token).ConfigureAwait(false);}
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);await errors.ConfigureAwait(false);
            if(process.ExitCode!=0||bytes.Length==0)return null;bytes.Position=0;token.ThrowIfCancellationRequested();return new Bitmap(bytes);
            }
            finally
            {
                try{if(!process.HasExited)process.Kill(true);}catch{}
                try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);}catch{}
                try{await errors.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);}catch{}
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch{return null;}
        finally{Workers.Release();}
    }
}
