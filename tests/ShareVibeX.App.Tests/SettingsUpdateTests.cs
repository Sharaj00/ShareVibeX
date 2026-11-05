using ShareVibeX.App.Startup;
using ShareVibeX.Core.Localization;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

public sealed class SettingsUpdateTests
{
    [Fact]
    public async Task LanguageSaveDoesNotRetryUnchangedUnavailableShortcutOrAutostart()
    {
        var old=new AppSettings();AppSettings? saved=null;
        await SettingsUpdate.SaveAsync(old,old,old with{Language=AppLanguage.Russian},_=>throw new Exception("Unavailable shortcut"),s=>{saved=s;return Task.CompletedTask;},(_,_)=>throw new Exception("Conflicting startup value"));
        Assert.NotNull(saved);Assert.Equal(AppLanguage.Russian,saved.Language);
    }
    [Fact]
    public async Task ChangedHotkeyFailureDoesNotPersistOrChangeAutostart()
    {
        var old=new AppSettings();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>SettingsUpdate.SaveAsync(old,old,old with{CaptureHotkey="Alt+F8"},_=>throw new InvalidOperationException("Unavailable shortcut"),_=>throw new Exception("Must not persist"),(_,_)=>throw new Exception("Must not change autostart")));
    }
    [Fact]
    public async Task ExplicitAutostartChangeUsesRegistrationTransaction()
    {
        var old=new AppSettings();var called=false;var saved=false;
        await SettingsUpdate.SaveAsync(old,old,old with{StartWithWindows=true},_=>{},_=>{saved=true;return Task.CompletedTask;},async(enabled,persist)=>{Assert.True(enabled);called=true;await persist();});
        Assert.True(called);Assert.True(saved);
    }
}
