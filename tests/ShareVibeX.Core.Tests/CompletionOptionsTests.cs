using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class CompletionOptionsTests
{
    [Fact]
    public async Task DefaultsAreSilentAndPreferencesRoundTrip()
    {
        Assert.False(new AppSettings().CompletionDefaults.PlaySound);Assert.False(new AppSettings().CompletionDefaults.ShowNotifications);
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-completion-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new PortableSettingsStore(root);var options=new CompletionOptions{CustomSoundPath="sounds/done.wav",LeftAction=CompletionClickAction.OpenFile,MiddleAction=CompletionClickAction.OpenFolder,ShowNotifications=true,PlaySound=true,DurationSeconds=5,FadeSeconds=0.5,Placement=CompletionPlacement.TopLeft};
            await store.SaveAsync(new AppSettings(Completion:options));Assert.Equal(options,(await store.LoadAsync()).CompletionDefaults);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(0,0)] [InlineData(31,1)] [InlineData(1,2)] [InlineData(3,double.NaN)]
    public async Task InvalidNotificationTimingCannotBePersisted(int duration,double fade)
    {
        var store=new PortableSettingsStore(Path.GetTempPath());
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.SaveAsync(new AppSettings(Completion:new(){DurationSeconds=duration,FadeSeconds=fade})));
    }
    [Fact] public async Task UnknownMouseActionCannotBePersisted()
    {await Assert.ThrowsAsync<InvalidDataException>(()=>new PortableSettingsStore(Path.GetTempPath()).SaveAsync(new AppSettings(Completion:new(){LeftAction=(CompletionClickAction)99})));}
}
