using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows.Capture;

namespace ShareVibeX.App.Tests;

public sealed class WgcFrameReadbackTests
{
    [Fact] public void PaddedRowsBecomeIndependentPackedOpaqueBgra()
    {
        byte[] source=[1,2,3,0,4,5,6,1,99,99,99,99,7,8,9,2,10,11,12,3,88,88,88,88];
        var result=WgcFrameReadback.CopyBgraRows(source,2,2,12);
        Assert.Equal(2,result.Width);Assert.Equal(2,result.Height);Assert.Equal(8,result.Stride);
        Assert.Equal(new byte[]{1,2,3,255,4,5,6,255,7,8,9,255,10,11,12,255},result.Bgra32);
        Array.Fill(source,(byte)0);Assert.Equal(1,result.Bgra32[0]);
    }
    [Theory]
    [InlineData(0,2,8,16)] [InlineData(2,0,8,16)] [InlineData(2,2,7,16)]
    [InlineData(2,2,8,15)] [InlineData(2,2,-8,16)] [InlineData(int.MaxValue,2,8,16)]
    public void InvalidGeometryOrBufferDoesNotProduceAFrame(int width,int height,int pitch,int length)
    {
        var error=Record.Exception(()=>WgcFrameReadback.CopyBgraRows(new byte[length],width,height,pitch));
        Assert.NotNull(error);Assert.IsNotType<CaptureSourceUnavailableException>(error);
    }
    [Fact] public void CancellationStopsCpuCopy()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>WgcFrameReadback.CopyBgraRows(new byte[16],2,2,8,cancellation.Token));
    }
}
