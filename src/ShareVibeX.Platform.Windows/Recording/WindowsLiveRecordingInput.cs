using NAudio.CoreAudioApi;
using NAudio.Wave;
using ShareVibeX.Core.Recording;
using System.Runtime.InteropServices;

namespace ShareVibeX.Platform.Windows.Recording;

internal sealed class WindowsLiveRecordingInput:ILiveRecordingInput
{
    private readonly MMDevice device;
    private readonly WasapiCapture capture;
    private readonly TaskCompletionSource ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool active;
    private int disposed;
    public RecordingAudioBuffer Buffer {get;}=new(48000*4);
    public Task Completion=>ended.Task;
    public void Activate()=>active=true;
    public WindowsLiveRecordingInput(RecordingOptions options,RecordingAudioDeviceKind kind)
    {
        using var enumerator=new MMDeviceEnumerator();var playback=kind==RecordingAudioDeviceKind.Playback;
        var id=playback?options.PlaybackDeviceId:options.MicrophoneDeviceId;var flow=playback?DataFlow.Render:DataFlow.Capture;
        device=string.IsNullOrWhiteSpace(id)?enumerator.GetDefaultAudioEndpoint(flow,Role.Multimedia):enumerator.GetDevice(id);
        try
        {
            if(device.State!=DeviceState.Active || device.DataFlow!=flow)throw new IOException(Loc.T("The selected audio endpoint is inactive or has the wrong direction."));
            capture=playback?new WasapiLoopbackCapture(device):new WasapiCapture(device);
            try
            {
                var converter=new RecordingAudioConverter(capture.WaveFormat);
                capture.DataAvailable+=(_,e)=>
                {
                    if(!active||Volatile.Read(ref disposed)!=0)return;
                    try{var samples=new List<float>();converter.Convert(e.Buffer.AsSpan(0,e.BytesRecorded),samples.Add);Buffer.Write(CollectionsMarshal.AsSpan(samples));}
                    catch(Exception ex){ended.TrySetException(ex);}
                };
                capture.RecordingStopped+=(_,e)=>{if(Volatile.Read(ref disposed)==0)ended.TrySetException(e.Exception??new IOException(Loc.T("The Windows audio input stopped unexpectedly.")));};
                capture.StartRecording();
            }
            catch{capture.Dispose();throw;}
        }
        catch{device.Dispose();throw;}
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;active=false;
        try{await Task.Run(()=>{try{capture.Dispose();}finally{device.Dispose();}}).ConfigureAwait(false);}
        finally{Buffer.Clear();ended.TrySetResult();}
    }
}
