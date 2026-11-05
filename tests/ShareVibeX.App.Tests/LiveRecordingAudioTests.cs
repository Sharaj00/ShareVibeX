using ShareVibeX.Platform.Windows.Recording;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Tests;

public sealed class LiveRecordingAudioTests
{
    [Fact]
    public async Task BothSourcesOffOpensNoDeviceAndMixesSilence()
    {
        await using var inputs=new LiveRecordingInputs((_,_)=>throw new InvalidOperationException("No device should open."));
        await inputs.InitializeAsync(new(false,false),default);
        var samples=new float[]{1,2,3,4};RecordingAudioBuffer.Mix(inputs.Buffers,samples);
        Assert.All(samples,s=>Assert.Equal(0,s));Assert.False(inputs.MicrophoneEnabled);
    }
    [Fact]
    public async Task ToggleOpensOnceAndOffReleasesAndDropsQueuedMicrophoneSamples()
    {
        var opened=0;var mic=new Input();
        await using var inputs=new LiveRecordingInputs((_,_)=>{opened++;return Task.FromResult<ILiveRecordingInput>(mic);});
        await inputs.InitializeAsync(new(false,false),default);inputs.Activate();
        await inputs.SetMicrophoneEnabledAsync(true,default);await inputs.SetMicrophoneEnabledAsync(true,default);
        mic.Buffer.Write(new float[]{.5f,.5f});Assert.True(inputs.MicrophoneEnabled);Assert.Equal(1,opened);Assert.True(mic.Active);
        await inputs.SetMicrophoneEnabledAsync(false,default);Assert.True(mic.Disposed);Assert.Empty(inputs.Buffers);
        var samples=new float[2];RecordingAudioBuffer.Mix(new[]{mic.Buffer},samples);Assert.All(samples,s=>Assert.Equal(0,s));
    }
    [Fact]
    public async Task FailedEnablePreservesPlaybackAndPreviousState()
    {
        var playback=new Input();
        await using var inputs=new LiveRecordingInputs((kind,_)=>kind==RecordingAudioDeviceKind.Playback?Task.FromResult<ILiveRecordingInput>(playback):Task.FromException<ILiveRecordingInput>(new IOException("mic denied")));
        await inputs.InitializeAsync(new(true,false),default);
        await Assert.ThrowsAsync<IOException>(()=>inputs.SetMicrophoneEnabledAsync(true,default));
        Assert.False(inputs.MicrophoneEnabled);Assert.False(playback.Disposed);Assert.Single(inputs.Buffers);
    }
    [Fact]
    public async Task CanceledEnableDisposesLateDeviceAndNeverPublishesIt()
    {
        var pending=new TaskCompletionSource<ILiveRecordingInput>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var inputs=new LiveRecordingInputs((_,_)=>pending.Task);await inputs.InitializeAsync(new(false,false),default);
        using var cancel=new CancellationTokenSource();var enabling=inputs.SetMicrophoneEnabledAsync(true,cancel.Token);cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>enabling);
        var mic=new Input();pending.SetResult(mic);await mic.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(inputs.MicrophoneEnabled);Assert.Empty(inputs.Buffers);
    }
    [Fact]
    public async Task ActiveDeviceFailureIsReportedButDisabledDeviceCompletionIsIgnored()
    {
        var mic=new Input();await using var inputs=new LiveRecordingInputs((_,_)=>Task.FromResult<ILiveRecordingInput>(mic));
        await inputs.InitializeAsync(new(false,true),default);await inputs.SetMicrophoneEnabledAsync(false,default);
        Assert.False(inputs.Failure.IsCompleted);
        var broken=new Input();await using var active=new LiveRecordingInputs((_,_)=>Task.FromResult<ILiveRecordingInput>(broken));
        await active.InitializeAsync(new(false,true),default);broken.Ended.SetException(new IOException("unplugged"));
        await Assert.ThrowsAsync<IOException>(()=>active.Failure.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    [Fact]
    public async Task FailedMicrophoneReleaseFailsTheStreamInsteadOfContinuingUnconfirmed()
    {
        var mic=new Input{FailDispose=true};await using var inputs=new LiveRecordingInputs((_,_)=>Task.FromResult<ILiveRecordingInput>(mic));
        await inputs.InitializeAsync(new(false,true),default);
        await Assert.ThrowsAsync<IOException>(()=>inputs.SetMicrophoneEnabledAsync(false,default));
        Assert.True(inputs.Failure.IsFaulted);
    }
    [Fact]
    public async Task FailedCandidateCleanupDoesNotHoldTheTransitionSemaphore()
    {
        var mic=new Input{FailActivate=true,FailDispose=true};
        var inputs=new LiveRecordingInputs((_,_)=>Task.FromResult<ILiveRecordingInput>(mic));
        await inputs.InitializeAsync(new(false,false),default);inputs.Activate();
        await Assert.ThrowsAsync<IOException>(()=>inputs.SetMicrophoneEnabledAsync(true,default));
        await inputs.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }
    private sealed class Input:ILiveRecordingInput
    {
        public RecordingAudioBuffer Buffer {get;}=new(16);public bool Active,Disposed,FailDispose,FailActivate;
        public TaskCompletionSource Ended=new(TaskCreationOptions.RunContinuationsAsynchronously);public Task Completion=>Ended.Task;
        public TaskCompletionSource DisposedSignal=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Activate(){if(FailActivate)throw new InvalidOperationException("activation failed");Active=true;}
        public ValueTask DisposeAsync(){Disposed=true;Buffer.Clear();Ended.TrySetResult();DisposedSignal.TrySetResult();return FailDispose?ValueTask.FromException(new IOException("release failed")):ValueTask.CompletedTask;}
    }
}
