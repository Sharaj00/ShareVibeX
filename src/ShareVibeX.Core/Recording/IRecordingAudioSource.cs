namespace ShareVibeX.Core.Recording;

public enum RecordingAudioDeviceKind { Playback, Microphone }
public sealed record RecordingAudioDevice(string Id, string Name, RecordingAudioDeviceKind Kind, bool IsDefault);
public sealed class RecordingAudioConsumerClosedException(string message,Exception? inner=null):IOException(message,inner) { }

public interface IRecordingAudioSource : IAsyncDisposable
{
    IReadOnlyList<string> Arguments { get; }
    Task Completion { get; }
}

public interface IRecordingAudioFactory
{
    Task<IRecordingAudioSource> OpenAsync(RecordingOptions options, CancellationToken token);
    Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token);
}
public interface ILiveRecordingAudioSource:IRecordingAudioSource
{
    bool MicrophoneEnabled {get;}
    Task SetMicrophoneEnabledAsync(bool enabled,CancellationToken token);
}
public interface ILiveRecordingAudioFactory:IRecordingAudioFactory
{
    Task<ILiveRecordingAudioSource> OpenLiveAsync(RecordingOptions options,RecordingSourceChoices sources,CancellationToken token);
}
