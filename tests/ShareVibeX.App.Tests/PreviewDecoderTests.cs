using ShareVibeX.Core.Playback;
using ShareVibeX.App.Playback;

namespace ShareVibeX.App.Tests;

public sealed class PreviewDecoderTests
{
    [Fact]
    public void DecoderCommandsSeekTheSameFileWithoutOpeningCaptureSources()
    {
        var media=new PreviewMedia("C:/recorded clip.mp4",null,TimeSpan.FromSeconds(10),1920,1080,60,true);
        var format=PreviewFormat.Fit(1920,1080,60);Assert.Equal(new PreviewFormat(960,540,30),format);
        var video=PreviewDecoderCommand.Video(media,format,TimeSpan.FromSeconds(2));
        var audio=PreviewDecoderCommand.Audio(media,TimeSpan.FromSeconds(2));
        Assert.Contains("2",video);Assert.Contains("2",audio);Assert.Contains(media.Path,video);Assert.Contains(media.Path,audio);
        Assert.Contains("rawvideo",video);Assert.Contains("bgra",video);Assert.Contains("f32le",audio);Assert.Contains("48000",audio);
        Assert.DoesNotContain("gdigrab",video);Assert.DoesNotContain("desktop",video);Assert.Contains("-nostdin",video);
    }
    [Fact]
    public async Task FrameReaderHandlesFragmentedDataAndRejectsTruncatedFrames()
    {
        var format=new PreviewFormat(2,1,30);
        using var frame=await PreviewFrameReader.ReadAsync(new Fragmented(new byte[]{1,2,3,255,4,5,6,255}),format,default);
        Assert.NotNull(frame);Assert.Equal(8,frame.Pixels.Length);Assert.Equal((byte)4,frame.Pixels.Span[4]);
        Assert.Null(await PreviewFrameReader.ReadAsync(new MemoryStream(),format,default));
        await Assert.ThrowsAsync<InvalidDataException>(()=>PreviewFrameReader.ReadAsync(new MemoryStream(new byte[7]),format,default));
        frame.Dispose();Assert.Throws<ObjectDisposedException>(()=>frame.Pixels);
    }
    [Theory]
    [InlineData(0,10)] [InlineData(10,-1)] [InlineData(int.MaxValue,int.MaxValue)]
    public void InvalidPreviewDimensionsAreRejected(int width,int height)=>Assert.ThrowsAny<ArgumentException>(()=>PreviewFormat.Fit(width,height,30));
    private sealed class Fragmented(byte[] bytes):MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>base.ReadAsync(buffer[..Math.Min(3,buffer.Length)],token);
    }
}
