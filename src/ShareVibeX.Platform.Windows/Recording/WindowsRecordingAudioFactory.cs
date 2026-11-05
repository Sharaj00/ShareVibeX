using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Platform.Windows.Recording;

public sealed class WindowsRecordingAudioFactory : ILiveRecordingAudioFactory
{
    public async Task<ILiveRecordingAudioSource> OpenLiveAsync(RecordingOptions options,RecordingSourceChoices sources,CancellationToken token)
    {
        if(options.AudioSource==AudioSource.DirectShow)throw new NotSupportedException(Loc.T("DirectShow does not support live native microphone control."));
        var inputs=new LiveRecordingInputs((kind,ct)=>Task.Run<ILiveRecordingInput>(()=>new WindowsLiveRecordingInput(options,kind),ct));
        try{await inputs.InitializeAsync(sources,token).ConfigureAwait(false);return new WindowsLiveRecordingAudioSource(inputs,token);}
        catch{await inputs.DisposeAsync().ConfigureAwait(false);throw;}
    }
    public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.AudioSource == AudioSource.DirectShow)
            throw new NotSupportedException(Loc.T("DirectShow audio must be opened by FFmpeg."));
        if (!Enum.IsDefined(options.AudioSource)) throw new ArgumentOutOfRangeException(nameof(options));
        // NAudio captures the current SynchronizationContext. Use a worker so device-stop events
        // cannot wait behind a UI thread that is awaiting recording cleanup.
        return Task.Run<IRecordingAudioSource>(() => new WindowsRecordingAudioSource(options, token), token);
    }

    public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token) =>
        Task.Run<IReadOnlyList<RecordingAudioDevice>>(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = new List<RecordingAudioDevice>();
            foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
            {
                token.ThrowIfCancellationRequested();
                string? defaultId = null;
                try
                {
                    using var defaultDevice = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                    defaultId = defaultDevice.ID;
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x80070490)) { }
                foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                    using (device)
                    {
                        token.ThrowIfCancellationRequested();
                        devices.Add(new(device.ID, device.FriendlyName,
                            flow == DataFlow.Render ? RecordingAudioDeviceKind.Playback : RecordingAudioDeviceKind.Microphone,
                            device.ID == defaultId));
                    }
            }
            return devices;
        }, token);
}

internal sealed class WindowsRecordingAudioSource : IRecordingAudioSource
{
    private readonly CancellationTokenSource lifetime;
    private readonly TaskCompletionSource<Exception> failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(MMDevice Device, WasapiCapture Capture, RecordingAudioBuffer Buffer)> inputs = [];
    private readonly NamedPipeServerStream pipe;
    private readonly Task pump;
    private readonly object disposeGate = new();
    private Task? disposeTask;
    private volatile bool accepting;
    private int stopping;

    public IReadOnlyList<string> Arguments { get; }
    public Task Completion => pump;

    public WindowsRecordingAudioSource(RecordingOptions options, CancellationToken token)
    {
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        var name = $"ShareVibeX-audio-{Guid.NewGuid():N}";
        pipe = new NamedPipeServerStream(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 0, 48000 * 8);
        Arguments = ["-thread_queue_size", "1024", "-f", "f32le", "-ar", "48000", "-ac", "2", "-probesize", "32", "-analyzeduration", "0", "-i", @"\\.\pipe\" + name];
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (options.AudioSource is AudioSource.System or AudioSource.SystemAndMicrophone)
                AddInput(enumerator, DataFlow.Render, options.PlaybackDeviceId);
            if (options.AudioSource is AudioSource.Microphone or AudioSource.SystemAndMicrophone)
                AddInput(enumerator, DataFlow.Capture, options.MicrophoneDeviceId);
            token.ThrowIfCancellationRequested();
            foreach (var input in inputs) input.Capture.StartRecording();
            pump = RunAsync();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref stopping, 1);
            foreach (var input in inputs)
            {
                try { input.Capture.Dispose(); } catch { }
                input.Device.Dispose();
            }
            pipe.Dispose();
            lifetime.Dispose();
            if (ex is OperationCanceledException) throw;
            throw new InvalidOperationException(Loc.F("The selected Windows audio endpoint could not be opened. Check the device and microphone permissions. {0}",ex.Message), ex);
        }
    }

    private void AddInput(MMDeviceEnumerator enumerator, DataFlow flow, string? id)
    {
        var device = string.IsNullOrWhiteSpace(id)
            ? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)
            : enumerator.GetDevice(id);
        WasapiCapture? capture = null;
        try
        {
            if (device.DataFlow != flow || device.State != DeviceState.Active)
                throw new InvalidOperationException(Loc.T("The selected audio endpoint is inactive or has the wrong direction."));
            capture = flow == DataFlow.Render ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
            var converter = new RecordingAudioConverter(capture.WaveFormat);
            var buffer = new RecordingAudioBuffer(48000 * 2 * 2);
            capture.DataAvailable += (_, e) =>
            {
                if (!accepting || Volatile.Read(ref stopping) != 0) return;
                try
                {
                    var packet = new List<float>();
                    converter.Convert(e.Buffer.AsSpan(0, e.BytesRecorded), packet.Add);
                    buffer.Write(CollectionsMarshal.AsSpan(packet));
                }
                catch (Exception ex) { Fail(ex); }
            };
            capture.RecordingStopped += (_, e) =>
            {
                if (Volatile.Read(ref stopping) == 0)
                    Fail(new InvalidOperationException("The Windows audio device stopped unexpectedly.", e.Exception));
            };
            inputs.Add((device, capture, buffer));
        }
        catch
        {
            capture?.Dispose();
            device.Dispose();
            throw;
        }
    }

    private void Fail(Exception exception)
    {
        if (failure.TrySetResult(exception)) lifetime.Cancel();
    }

    private async Task RunAsync()
    {
        try
        {
            // Return from OpenAsync before connection: FFmpeg is the client started by the caller.
            await pipe.WaitForConnectionAsync(lifetime.Token).ConfigureAwait(false);
            accepting = true;
            var clock = Stopwatch.StartNew();
            long framesSent = 0;
            var samples = new float[480 * 2];
            var bytes = new byte[samples.Length * sizeof(float)];
            var buffers = inputs.Select(input => input.Buffer).ToArray();
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                // 100ms lets normal WASAPI callback batching arrive before its samples are needed.
                var due = TimeSpan.FromSeconds((framesSent + 480) / 48000d + .1);
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, lifetime.Token).ConfigureAwait(false);
                if (clock.Elapsed - due > TimeSpan.FromSeconds(2))
                    throw new InvalidOperationException(Loc.T("The audio output fell more than two seconds behind its sample clock."));
                RecordingAudioBuffer.Mix(buffers, samples);
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                using (var writeDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    writeDeadline.CancelAfter(TimeSpan.FromSeconds(2));
                    try { await pipe.WriteAsync(bytes, writeDeadline.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                        throw new TimeoutException(Loc.T("FFmpeg did not consume the recording audio for two seconds."));
                    }
                }
                framesSent += 480;
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            if (failure.Task.IsCompletedSuccessfully) throw await failure.Task.ConfigureAwait(false);
        }
        catch(IOException error)when(!lifetime.IsCancellationRequested)
        {
            throw new RecordingAudioConsumerClosedException(Loc.T("FFmpeg closed its audio input."),error);
        }
        finally
        {
            accepting = false;
            Interlocked.Exchange(ref stopping, 1);
            foreach (var input in inputs) input.Capture.StopRecording();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposeGate) return new ValueTask(disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref stopping, 1);
        lifetime.Cancel();
        try { await pump.ConfigureAwait(false); }
        catch { /* Completion retains the failure for the recording session. */ }
        // WasapiCapture.Dispose joins its capture thread; keep this off the UI thread.
        try
        {
            await Task.Run(() =>
            {
                var errors = new List<Exception>();
                foreach (var input in inputs)
                {
                    try { input.Capture.Dispose(); } catch (Exception ex) { errors.Add(ex); }
                    try { input.Device.Dispose(); } catch (Exception ex) { errors.Add(ex); }
                }
                if (errors.Count != 0) throw new AggregateException(Loc.T("Windows audio cleanup failed."), errors);
            }).ConfigureAwait(false);
        }
        finally
        {
            pipe.Dispose();
            lifetime.Dispose();
        }
    }
}
