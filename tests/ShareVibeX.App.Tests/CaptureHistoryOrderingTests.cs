using ShareVibeX.App.History;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

public sealed class CaptureHistoryOrderingTests
{
    [Fact]
    public async Task FilenameCaptureDateOverridesModifiedTimeAndGroupsMonths()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-order-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"2026-11"));Directory.CreateDirectory(Path.Combine(root,"2026-10"));
            var old=Path.Combine(root,"2026-10","app_20261031_235959.png");var newer=Path.Combine(root,"2026-11","app_name_20261104_010203_2.mp4");
            await File.WriteAllTextAsync(old,"old");await File.WriteAllTextAsync(newer,"new");File.SetLastWriteTime(old,DateTime.Now.AddYears(1));File.SetLastWriteTime(newer,DateTime.Now.AddYears(-1));
            var history=new CaptureHistory(()=>new AppSettings(HistoryDirectory:root));var entries=await history.ListEntriesAsync();
            Assert.Equal(newer,entries[0].Path);Assert.Equal(new DateTime(2026,11,4,1,2,3),entries[0].CapturedAt);Assert.Equal("2026-11",entries[0].Month);Assert.Equal("2026-10",entries[1].Month);
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>history.ListEntriesAsync(cancelled.Token));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
