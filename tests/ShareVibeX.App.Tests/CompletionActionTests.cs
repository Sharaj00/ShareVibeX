using ShareVibeX.App.Notifications;
using ShareVibeX.Core.Settings;
namespace ShareVibeX.App.Tests;
public sealed class CompletionActionTests
{
    [Fact] public void CloseDoesNotOpenFiles(){var closed=0;Assert.True(CompletionActions.Execute(CompletionClickAction.Close,new("Done"),()=>closed++,_=>throw new Exception()));Assert.Equal(1,closed);}
    [Fact] public void FileActionWithoutFileKeepsNoticeOpen(){var closed=0;Assert.False(CompletionActions.Execute(CompletionClickAction.OpenFile,new("Copied"),()=>closed++,_=>throw new Exception()));Assert.Equal(0,closed);}
    [Fact] public void OpenFileUsesExactResultAndClosesOnce()
    {
        var path=Path.GetTempFileName();try{string? opened=null;var closed=0;Assert.True(CompletionActions.Execute(CompletionClickAction.OpenFile,new("Saved",path),()=>closed++,p=>opened=p));Assert.Equal(path,opened);Assert.Equal(1,closed);}finally{File.Delete(path);}
    }
}
