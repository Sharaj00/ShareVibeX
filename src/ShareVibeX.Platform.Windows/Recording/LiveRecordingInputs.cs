using ShareVibeX.Core.Recording;

namespace ShareVibeX.Platform.Windows.Recording;

internal interface ILiveRecordingInput:IAsyncDisposable
{
    RecordingAudioBuffer Buffer {get;}
    Task Completion {get;}
    void Activate();
}

internal sealed class LiveRecordingInputs(Func<RecordingAudioDeviceKind,CancellationToken,Task<ILiveRecordingInput>> open):IAsyncDisposable
{
    private readonly SemaphoreSlim transition=new(1);
    private readonly object gate=new();
    private readonly CancellationTokenSource lifetime=new();
    private readonly TaskCompletionSource failure=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ILiveRecordingInput? playback,microphone;
    private RecordingAudioBuffer[] buffers=[];
    private bool active,disposed;
    public RecordingAudioBuffer[] Buffers=>Volatile.Read(ref buffers);
    public bool MicrophoneEnabled {get{lock(gate)return microphone is not null;}}
    public Task Failure=>failure.Task;
    public async Task InitializeAsync(RecordingSourceChoices sources,CancellationToken token)
    {
        if(sources.Sound)
        {
            playback=await OpenAsync(RecordingAudioDeviceKind.Playback,token).ConfigureAwait(false);
            Publish();_=ObserveAsync(playback);
        }
        if(sources.Microphone)await SetMicrophoneEnabledAsync(true,token).ConfigureAwait(false);
    }
    public void Activate(){lock(gate){active=true;playback?.Activate();microphone?.Activate();}}
    public async Task SetMicrophoneEnabledAsync(bool enabled,CancellationToken token)
    {
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
        await transition.WaitAsync(cancellation.Token).ConfigureAwait(false);
        ILiveRecordingInput? candidate=null;
        try
        {
            lock(gate){ObjectDisposedException.ThrowIf(disposed,this);if(enabled==(microphone is not null))return;}
            if(enabled)
            {
                candidate=await OpenAsync(RecordingAudioDeviceKind.Microphone,cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                lock(gate){if(active)candidate.Activate();microphone=candidate;Publish();candidate=null;_=ObserveAsync(microphone);}
            }
            else
            {
                ILiveRecordingInput? old;
                lock(gate){old=microphone;microphone=null;Publish();}
                if(old is not null)
                {
                    old.Buffer.Clear();
                    try{await old.DisposeAsync().ConfigureAwait(false);}
                    catch(Exception error){failure.TrySetException(error);throw;}
                }
            }
        }
        finally
        {
            try{if(candidate is not null)await candidate.DisposeAsync().ConfigureAwait(false);}
            finally{transition.Release();}
        }
    }
    private void Publish()=>Volatile.Write(ref buffers,new[]{playback?.Buffer,microphone?.Buffer}.OfType<RecordingAudioBuffer>().ToArray());
    private async Task<ILiveRecordingInput> OpenAsync(RecordingAudioDeviceKind kind,CancellationToken token)
    {
        var pending=open(kind,token);
        try{return await pending.WaitAsync(token).ConfigureAwait(false);}
        catch{_=DisposeLateAsync(pending);throw;}
    }
    private static async Task DisposeLateAsync(Task<ILiveRecordingInput> pending)
    {try{await (await pending.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);}catch{ /* The caller already observes the open failure. */ }}
    private async Task ObserveAsync(ILiveRecordingInput input)
    {
        Exception? error=null;
        try{await input.Completion.ConfigureAwait(false);error=new IOException(Loc.T("An active audio input stopped unexpectedly."));}catch(Exception ex){error=ex;}
        lock(gate)if(!disposed && (ReferenceEquals(input,playback)||ReferenceEquals(input,microphone)))failure.TrySetException(error);
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();await transition.WaitAsync().ConfigureAwait(false);
        try
        {
            ILiveRecordingInput? first,second;
            lock(gate){if(disposed)return;disposed=true;first=playback;second=microphone;playback=microphone=null;Publish();}
            try{if(first is not null)await first.DisposeAsync().ConfigureAwait(false);}
            finally{if(second is not null)await second.DisposeAsync().ConfigureAwait(false);}
        }
        finally{transition.Release();}
    }
}
