using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Startup;

public static class StartupSettingsTransaction
{
    public static async Task SaveAsync(WindowsStartupRegistration registration,bool enabled,Func<Task> save)
    {
        var previous=registration.Read();registration.SetEnabled(enabled);var changed=previous!=registration.Read();
        try{await save();}
        catch(Exception failure)
        {
            try{if(changed)registration.Restore(previous);}
            catch(Exception rollback){throw new AggregateException(Loc.T("Settings could not be saved and the startup entry could not be restored. Check Windows Startup apps."),failure,rollback);}
            throw;
        }
    }
}
