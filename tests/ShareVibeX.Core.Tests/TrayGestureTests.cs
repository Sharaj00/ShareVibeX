using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class TrayGestureTests
{
    [Fact] public void SingleWaitsForDoubleClickInterval()
    {
        var router=new TrayGestureRouter(500);router.LeftUp(100);
        Assert.Null(router.Advance(599));Assert.Equal(TrayGesture.Single,router.Advance(600));Assert.Null(router.Advance(900));
    }
    [Fact] public void DoubleSuppressesBothSingleClicks()
    {
        var router=new TrayGestureRouter(500);router.LeftUp(0);
        Assert.Equal(TrayGesture.Double,router.DoubleClick());router.LeftUp(120);
        Assert.Null(router.Advance(1000));
    }
    [Fact] public void ContextMenuCancelsPendingSingle()
    {
        var router=new TrayGestureRouter(500);router.LeftUp(0);router.Cancel();Assert.Null(router.Advance(1000));
    }
    [Fact] public async Task TrayPreferencesPersistAndRejectUnknownActions()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-tray-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new PortableSettingsStore(root);var settings=new AppSettings(TraySingleClick:TrayAction.None,TrayDoubleClick:TrayAction.OpenSettings,TrayMiddleClick:TrayAction.StopRecording);
            await store.SaveAsync(settings);var loaded=await store.LoadAsync();Assert.Equal(settings,loaded);
            await Assert.ThrowsAsync<InvalidDataException>(()=>store.SaveAsync(settings with{TrayMiddleClick=(TrayAction)99}));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}

