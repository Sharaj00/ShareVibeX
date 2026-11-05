using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class LegacyHistoryActionTests
{
    [Theory]
    [InlineData("\"OpenHistory\"")]
    [InlineData("4")]
    public async Task RemovedHistoryTrayActionMigratesWithoutChangingOtherSettings(string action)
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-tray-migration-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);var path=Path.Combine(root,"settings.json");
            var json=$"{{\"SchemaVersion\":2,\"TraySingleClick\":{action},\"TrayDoubleClick\":{action},\"TrayMiddleClick\":{action},\"CaptureHotkey\":\"Alt+F8\"}}";
            await File.WriteAllTextAsync(path,json);var result=await new PortableSettingsStore(root).LoadAsync();
            Assert.Equal(TrayAction.OpenSettings,result.TraySingleClick);Assert.Equal(TrayAction.OpenSettings,result.TrayDoubleClick);Assert.Equal(TrayAction.OpenSettings,result.TrayMiddleClick);
            Assert.Equal("Alt+F8",result.CaptureHotkey);Assert.Equal(json,await File.ReadAllTextAsync(path));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
