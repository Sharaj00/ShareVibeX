using ShareVibeX.Core.Recording;
namespace ShareVibeX.App.Recording;

public sealed class MicrophoneAvailability(IRecordingAudioFactory audio)
{
    public const string UnavailableMessage="No available recording device was found.";
    public async Task<bool> CheckAsync(RecordingOptions options,CancellationToken token)
    {
        if(options.AudioSource==AudioSource.DirectShow)return false;
        try
        {
            var devices=await audio.GetDevicesAsync(token);token.ThrowIfCancellationRequested();
            return devices.Any(device=>device.Kind==RecordingAudioDeviceKind.Microphone&&
                (string.IsNullOrWhiteSpace(options.MicrophoneDeviceId)?device.IsDefault:device.Id==options.MicrophoneDeviceId));
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception error){DiagnosticLog.Write("Microphone availability: "+error.Message);return false;}
    }
}
