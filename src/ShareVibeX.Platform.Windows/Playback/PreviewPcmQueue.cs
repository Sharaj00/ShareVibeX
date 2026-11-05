using NAudio.Wave;

namespace ShareVibeX.Platform.Windows.Playback;

internal sealed class PreviewPcmQueue:IWaveProvider,IDisposable
{
    private readonly byte[] bytes;
    private readonly object gate=new();private int head,count;private bool complete,disposed;
    public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
    public int BufferedBytes {get{lock(gate)return count;}}
    public long TotalWritten {get;private set;}
    public bool IsComplete {get{lock(gate)return complete;}}
    public PreviewPcmQueue(int capacity=192000){if(capacity<8||capacity%8!=0)throw new ArgumentOutOfRangeException(nameof(capacity));bytes=new byte[capacity];}
    public async Task WriteAsync(ReadOnlyMemory<byte> pcm,CancellationToken token)
    {
        if(pcm.Length%8!=0)throw new ArgumentException(Loc.T("PCM must contain complete float stereo frames."));var offset=0;
        do
        {
            token.ThrowIfCancellationRequested();var written=0;
            lock(gate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);if(complete)throw new IOException(Loc.T("Audio input has already ended."));
                written=Math.Min(pcm.Length-offset,bytes.Length-count);
                for(var i=0;i<written;i++)bytes[(head+count+i)%bytes.Length]=pcm.Span[offset+i];
                count+=written;TotalWritten+=written;if(written>0)Monitor.PulseAll(gate);
            }
            offset+=written;if(offset<pcm.Length)await Task.Delay(5,token).ConfigureAwait(false);
        }while(offset<pcm.Length);
    }
    public int Read(byte[] destination,int offset,int length)
    {
        lock(gate)
        {
            var read=0;
            while(read<length)
            {
                if(disposed)return 0;
                if(count==0)
                {
                    if(complete)break;
                    if(!Monitor.Wait(gate,300))throw new IOException(Loc.T("Preview audio underrun. Retry playback."));continue;
                }
                var available=Math.Min(length-read,count);
                for(var i=0;i<available;i++){destination[offset+read+i]=bytes[head];head=(head+1)%bytes.Length;}
                count-=available;read+=available;
            }
            return read;
        }
    }
    public void Complete(){lock(gate){complete=true;Monitor.PulseAll(gate);}}
    public void Dispose(){lock(gate){disposed=true;count=0;Array.Clear(bytes);Monitor.PulseAll(gate);}}
}
