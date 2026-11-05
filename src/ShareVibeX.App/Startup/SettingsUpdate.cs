using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Startup;

public static class SettingsUpdate
{
    public static async Task SaveAsync(AppSettings previous,AppSettings displayed,AppSettings proposed,Action<AppSettings> applyHotkey,Func<AppSettings,Task> persist,Func<bool,Func<Task>,Task> changeStartup)
    {
        var hotkeyChanged=!string.Equals(previous.CaptureHotkey,proposed.CaptureHotkey,StringComparison.Ordinal);
        var applied=false;
        try
        {
            if(hotkeyChanged){applyHotkey(proposed);applied=true;}
            if(displayed.StartWithWindows!=proposed.StartWithWindows)
                await changeStartup(proposed.StartWithWindows,()=>persist(proposed));
            else await persist(proposed);
        }
        catch
        {
            if(applied)applyHotkey(previous);
            throw;
        }
    }
}
