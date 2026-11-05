using ShareVibeX.App.Startup;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;

public sealed class StartupSettingsTests
{
    [Fact]
    public void RegistrationIsReadOnlyUntilExplicitlyChanged()
    {
        string? value=null;var writes=0;
        var registration=new WindowsStartupRegistration(@"C:\Tools\ShareVibeX.exe",()=>value,v=>{value=v;writes++;});
        Assert.False(registration.IsEnabled);Assert.Equal(0,writes);
        registration.SetEnabled(true);Assert.Equal("\"C:\\Tools\\ShareVibeX.exe\"",value);Assert.True(registration.IsEnabled);
        registration.SetEnabled(false);Assert.Null(value);
    }
    [Fact]
    public async Task FailedPortableSaveRestoresPriorStartupValue()
    {
        string? value=@"""C:\Old\ShareVibeX.exe""";
        var original=value;var registration=new WindowsStartupRegistration(@"C:\New\ShareVibeX.exe",()=>value,v=>value=v);
        await Assert.ThrowsAsync<IOException>(()=>StartupSettingsTransaction.SaveAsync(registration,true,()=>throw new IOException("disk full")));
        Assert.Equal(original,value);
    }
    [Fact]
    public void ConflictingStartupValueCannotBeOverwrittenOrDeleted()
    {
        string? value="unrelated.exe";var registration=new WindowsStartupRegistration(@"C:\Tools\ShareVibeX.exe",()=>value,v=>value=v);
        Assert.Throws<InvalidOperationException>(()=>registration.SetEnabled(true));Assert.Throws<InvalidOperationException>(()=>registration.SetEnabled(false));Assert.Equal("unrelated.exe",value);
    }
}
