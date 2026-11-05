using System.Buffers;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.App.Playback;

public static class PreviewFrameReader
{
    public static async Task<PreviewFrame?> ReadAsync(Stream stream,PreviewFormat format,CancellationToken token)
    {
        if(format.Width<=0||format.Height<=0||format.Width>960||format.Height>540)throw new ArgumentOutOfRangeException(nameof(format));
        var bytes=ArrayPool<byte>.Shared.Rent(format.ByteLength);var transferred=false;
        try
        {
            var count=0;
            while(count<format.ByteLength)
            {
                var read=await stream.ReadAsync(bytes.AsMemory(count,format.ByteLength-count),token).ConfigureAwait(false);
                if(read==0){if(count==0)return null;throw new InvalidDataException(Loc.T("The video decoder returned an incomplete frame."));}count+=read;
            }
            transferred=true;return new(bytes,format);
        }
        finally{if(!transferred)ArrayPool<byte>.Shared.Return(bytes);}
    }
}
