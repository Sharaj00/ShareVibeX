using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Recording;
using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Localization;

namespace ShareVibeX.App.Tests;

public sealed class MicrophoneAvailabilityTests
{
    [Fact] public async Task MissingDefaultAndDisconnectedConfiguredMicrophoneAreUnavailable()
    {
        var audio=new Audio();var probe=new MicrophoneAvailability(audio);
        Assert.False(await probe.CheckAsync(new(),default));
        audio.Devices=[new("speakers","Playback",RecordingAudioDeviceKind.Playback,true)];Assert.False(await probe.CheckAsync(new(),default));
        audio.Devices=[new("mic","Mic",RecordingAudioDeviceKind.Microphone,false)];Assert.False(await probe.CheckAsync(new(),default));
        Assert.True(await probe.CheckAsync(new(){MicrophoneDeviceId="mic"},default));
        Assert.False(await probe.CheckAsync(new(){MicrophoneDeviceId="unplugged"},default));
        audio.Devices=[new("mic","Mic",RecordingAudioDeviceKind.Microphone,true)];Assert.True(await probe.CheckAsync(new(),default));
        audio.Devices=[];Assert.False(await probe.CheckAsync(new(),default));
    }
    [Fact] public async Task EnumerationFailureDoesNotEscapeButCancellationDoes()
    {
        var audio=new Audio{Error=new IOException("Missing endpoint")};var probe=new MicrophoneAvailability(audio);
        Assert.False(await probe.CheckAsync(new(),default));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();audio.Error=new OperationCanceledException(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>probe.CheckAsync(new(),cancellation.Token));
    }
    private sealed class Audio:IRecordingAudioFactory
    {
        public IReadOnlyList<RecordingAudioDevice> Devices=[];public Exception? Error;
        public Task<IReadOnlyList<RecordingAudioDevice>> GetDevicesAsync(CancellationToken token)=>Error is {} error?Task.FromException<IReadOnlyList<RecordingAudioDevice>>(error):Task.FromResult(Devices);
        public Task<IRecordingAudioSource> OpenAsync(RecordingOptions options,CancellationToken token)=>throw new Exception("No device opening allowed");
    }
}

[Collection("Avalonia UI")]
public sealed class MicrophoneButtonTests(UiTestFixture ui)
{
    [Theory] [InlineData(AppLanguage.English)] [InlineData(AppLanguage.Russian)]
    public Task MissingMicrophoneIsUncheckedDisabledAndExplainedInBothPanels(AppLanguage language)=>ui.Run(()=>
    {
        var previous=Loc.Language;Loc.Language=language;
        var toolbar=new CaptureModeToolbar(()=>Task.CompletedTask,()=>{},()=>{},()=>{},_=>Task.CompletedTask,new(false,true));
        var recording=new RecordingControlWindow(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{},()=>Task.CompletedTask,_=>Task.CompletedTask);
        try
        {
            toolbar.SetMicrophoneAvailable(false);Assert.False(toolbar.Sources.Microphone);
            toolbar.SetSources(new(false,true));Assert.False(toolbar.Sources.Microphone);
            recording.SetMicrophoneAvailable(false);
            foreach(var panel in new Control[]{toolbar,recording})
            {
                var button=panel.GetLogicalDescendants().OfType<ToggleButton>().Single(b=>AutomationProperties.GetName(b)=="Microphone");
                Assert.False(button.IsEnabled);Assert.Equal(Loc.T(MicrophoneAvailability.UnavailableMessage),ToolTip.GetTip(button));Assert.True(ToolTip.GetShowOnDisabled(button));
            }
            toolbar.SetMicrophoneAvailable(true);Assert.False(toolbar.Sources.Microphone);
            Assert.True(toolbar.GetLogicalDescendants().OfType<ToggleButton>().Single(b=>AutomationProperties.GetName(b)=="Microphone").IsEnabled);
        }
        finally{recording.Dismiss();Loc.Language=previous;}
    });
}
