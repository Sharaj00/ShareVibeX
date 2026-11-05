using ShareVibeX.Core.Settings;
namespace ShareVibeX.App.Notifications;

public static class CompletionActions
{
    public static bool Execute(CompletionClickAction action,CompletionNotice notice,Action close,Action<string> open)
    {
        if(action==CompletionClickAction.None)return true;
        if(action==CompletionClickAction.Close){close();return true;}
        if(notice.Path is not {} path)return false;
        string? target=action switch{CompletionClickAction.OpenFile=>File.Exists(path)?path:null,CompletionClickAction.OpenFolder=>Path.GetDirectoryName(path),_=>null};
        if(target is null||action==CompletionClickAction.OpenFolder&&!Directory.Exists(target))return false;
        open(target);close();return true;
    }
}
