using System.Buffers;

namespace ShareVibeX.Core.Playback;

public sealed record PreviewMedia(string Path,string? FfmpegPath,TimeSpan? Duration,int Width,int Height,int FrameRate,bool HasAudio);
public readonly record struct PreviewFormat(int Width,int Height,int FrameRate)
{
    public int ByteLength=>checked(Width*Height*4);
    public static PreviewFormat Fit(int width,int height,int rate)
    {
        if(width<=0||height<=0||width>32768||height>32768||rate<=0||rate>240)throw new ArgumentOutOfRangeException(nameof(width));
        var scale=Math.Min(1,Math.Min(960d/width,540d/height));
        return new(Math.Max(1,(int)(width*scale)),Math.Max(1,(int)(height*scale)),Math.Min(30,rate));
    }
}
public sealed class PreviewFrame:IDisposable
{
    private byte[]? buffer;
    public PreviewFormat Format {get;}
    public ReadOnlyMemory<byte> Pixels=>buffer is {} bytes?bytes.AsMemory(0,Format.ByteLength):throw new ObjectDisposedException(nameof(PreviewFrame));
    public PreviewFrame(byte[] rented,PreviewFormat format){buffer=rented;Format=format;}
    public void Dispose(){var bytes=Interlocked.Exchange(ref buffer,null);if(bytes is not null)ArrayPool<byte>.Shared.Return(bytes);}
}
public interface IPreviewDecoder:IAsyncDisposable
{
    Stream Video {get;}
    Stream? Audio {get;}
    Task VideoCompletion {get;}
    Task AudioCompletion {get;}
}
public interface IPreviewDecoderFactory
{
    Task<IPreviewDecoder> OpenAsync(PreviewMedia media,PreviewFormat format,TimeSpan position,bool audio,CancellationToken token);
}
public interface IPreviewAudioOutput:IAsyncDisposable
{
    TimeSpan Position {get;}
    Task Failure {get;}
    Task WriteAsync(ReadOnlyMemory<byte> pcm,CancellationToken token);
    void Start();
    Task DrainAsync(CancellationToken token);
}
public interface IPreviewAudioOutputFactory
{
    Task<IPreviewAudioOutput> OpenAsync(CancellationToken token);
}
