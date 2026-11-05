using ShareVibeX.Core.Capture;

namespace ShareVibeX.App.Notifications;

public static class CompletionPreview
{
    public static CapturedFrame CreateFrame(CapturedFrame source)
    {
        var factor=Math.Min(1,Math.Min(480d/source.Width,270d/source.Height));
        var width=Math.Max(1,(int)Math.Round(source.Width*factor));var height=Math.Max(1,(int)Math.Round(source.Height*factor));
        var stride=width*4;var pixels=new byte[stride*height];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var sourceX=Math.Min(source.Width-1,(int)(x/factor));var sourceY=Math.Min(source.Height-1,(int)(y/factor));
            Buffer.BlockCopy(source.Bgra32,sourceY*source.Stride+sourceX*4,pixels,y*stride+x*4,4);
        }
        return new(width,height,stride,pixels);
    }
}
