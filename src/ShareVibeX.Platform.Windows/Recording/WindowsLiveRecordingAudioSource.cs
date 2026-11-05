using System.Diagnostics;
using System.IO.Pipes;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Platform.Windows.Recording;

internal sealed class WindowsLiveRecordingAudioSource:ILiveRecordingAudioSource
{
    private readonly LiveRecordingInputs inputs;
    private readonly CancellationTokenSource lifetime;
    private readonly NamedPipeServerStream pipe;
    private readonly Task pump;
    private readonly object gate=new();private Task? disposal;
    public IReadOnlyList<string> Arguments {get;}
    public Task Completion=>pump;
    public bool MicrophoneEnabled=>inputs.MicrophoneEnabled;
    public WindowsLiveRecordingAudioSource(LiveRecordingInputs inputs,CancellationToken token)
    {
        this.inputs=inputs;lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);var name="ShareVibeX-live-"+Guid.NewGuid().ToString("N");
        pipe=new(name,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,0,48000*8);
        Arguments=["-thread_queue_size","1024","-f","f32le","-ar","48000","-ac","2","-probesize","32","-analyzeduration","0","-i",@"\\.\pipe\"+name];
        pump=PumpAsync();_=WatchFailureAsync();
    }
    public async Task SetMicrophoneEnabledAsync(bool enabled,CancellationToken token)
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
        await inputs.SetMicrophoneEnabledAsync(enabled,cancel.Token).ConfigureAwait(false);
    }
    private async Task WatchFailureAsync()
    {
        try{await inputs.Failure.WaitAsync(lifetime.Token).ConfigureAwait(false);}
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
        catch{try{lifetime.Cancel();}catch(ObjectDisposedException){}}
    }
    private async Task PumpAsync()
    {
        try
        {
            await pipe.WaitForConnectionAsync(lifetime.Token).ConfigureAwait(false);inputs.Activate();var clock=Stopwatch.StartNew();long frames=0;
            var samples=new float[960];var bytes=new byte[3840];
            while(true)
            {
                lifetime.Token.ThrowIfCancellationRequested();var due=TimeSpan.FromSeconds((frames+480)/48000d+.1);var wait=due-clock.Elapsed;
                if(wait>TimeSpan.Zero)await Task.Delay(wait,lifetime.Token).ConfigureAwait(false);
                if(clock.Elapsed-due>TimeSpan.FromSeconds(2))throw new IOException(Loc.T("Live audio fell behind its sample clock."));
                RecordingAudioBuffer.Mix(inputs.Buffers,samples);System.Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(2));
                try{await pipe.WriteAsync(bytes,deadline.Token).ConfigureAwait(false);}
                catch(OperationCanceledException)when(!lifetime.IsCancellationRequested){throw new TimeoutException(Loc.T("FFmpeg stopped consuming live audio."));}
                frames+=480;
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){if(inputs.Failure.IsFaulted)await inputs.Failure.ConfigureAwait(false);}
        catch(IOException ex)when(!lifetime.IsCancellationRequested){throw new RecordingAudioConsumerClosedException(Loc.T("FFmpeg closed its live audio input."),ex);}
        finally{await inputs.DisposeAsync().ConfigureAwait(false);}
    }
    public ValueTask DisposeAsync(){lock(gate)return new(disposal??=DisposeCoreAsync());}
    private async Task DisposeCoreAsync()
    {
        lifetime.Cancel();try{await pump.ConfigureAwait(false);}catch{ /* Completion carries the run error. */ }
        finally{pipe.Dispose();lifetime.Dispose();}
    }
}
