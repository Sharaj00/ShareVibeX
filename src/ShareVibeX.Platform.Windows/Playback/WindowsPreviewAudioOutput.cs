using NAudio.CoreAudioApi;
using NAudio.Wave;
using ShareVibeX.Core.Playback;

namespace ShareVibeX.Platform.Windows.Playback;

public sealed class WindowsPreviewAudioOutputFactory:IPreviewAudioOutputFactory
{
    public async Task<IPreviewAudioOutput> OpenAsync(CancellationToken token)
    {
        var opening=Task.Run<IPreviewAudioOutput>(()=>new WindowsPreviewAudioOutput(),token);
        IPreviewAudioOutput? output=null;
        try{output=await opening.ConfigureAwait(false);token.ThrowIfCancellationRequested();return output;}
        catch{if(output is not null)await output.DisposeAsync().ConfigureAwait(false);throw;}
    }
}

internal sealed class WindowsPreviewAudioOutput:IPreviewAudioOutput
{
    private readonly PreviewPcmQueue queue=new();
    private readonly MMDevice device;
    private readonly WasapiOut player;
    private readonly TaskCompletionSource failure=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate=new();private bool requested,started,disposed;private Task? disposal;private long lastPosition;
    public Task Failure=>failure.Task;
    public WindowsPreviewAudioOutput()
    {
        using var enumerator=new MMDeviceEnumerator();device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
        try
        {
            player=new(device,AudioClientShareMode.Shared,true,100);
            try
            {
                player.Init(queue);
                player.PlaybackStopped+=(_,e)=>
                {
                    if(e.Exception is {} error)failure.TrySetException(error);
                    else if(!queue.IsComplete&&!disposed)failure.TrySetException(new IOException(Loc.T("Preview audio output stopped unexpectedly.")));
                    stopped.TrySetResult();
                };
            }
            catch{player.Dispose();throw;}
        }
        catch{device.Dispose();queue.Dispose();throw;}
    }
    public TimeSpan Position
    {
        get
        {
            lock(gate)
            {
                if(!disposed&&started)
                {
                    try
                    {
                        var seconds=stopped.Task.IsCompleted&&queue.IsComplete?queue.TotalWritten/384000d:player.GetPosition()/(double)player.OutputWaveFormat.AverageBytesPerSecond;
                        lastPosition=Math.Max(lastPosition,TimeSpan.FromSeconds(seconds).Ticks);
                    }
                    catch(Exception error){failure.TrySetException(error);}
                }
                return TimeSpan.FromTicks(lastPosition);
            }
        }
    }
    public async Task WriteAsync(ReadOnlyMemory<byte> pcm,CancellationToken token)
    {
        if(failure.Task.IsFaulted)await failure.Task.ConfigureAwait(false);
        await queue.WriteAsync(pcm,token).ConfigureAwait(false);TryStart();
    }
    public void Start(){lock(gate){ObjectDisposedException.ThrowIf(disposed,this);requested=true;}TryStart();}
    private void TryStart()
    {
        lock(gate)
        {
            if(disposed||started||!requested)return;
            if(queue.BufferedBytes<76800&&!queue.IsComplete)return;
            if(queue.BufferedBytes==0){if(queue.IsComplete)stopped.TrySetResult();return;}
            player.Play();started=true;
        }
    }
    public async Task DrainAsync(CancellationToken token)
    {
        queue.Complete();TryStart();await stopped.Task.WaitAsync(token).ConfigureAwait(false);
        if(failure.Task.IsFaulted)await failure.Task.ConfigureAwait(false);
    }
    public ValueTask DisposeAsync(){lock(gate)return new(disposal??=DisposeCoreAsync());}
    private async Task DisposeCoreAsync()
    {
        lock(gate)disposed=true;queue.Dispose();
        try{await Task.Run(()=>{try{player.Stop();}finally{try{player.Dispose();}finally{device.Dispose();}}}).ConfigureAwait(false);}
        finally{stopped.TrySetResult();}
    }
}
