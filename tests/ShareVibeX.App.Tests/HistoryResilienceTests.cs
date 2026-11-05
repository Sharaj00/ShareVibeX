using ShareVibeX.App.History;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.App.Tests;

public sealed class HistoryResilienceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnavailableEntryOrMonthDoesNotHideOtherCaptures(bool missingMonth)
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-resilience-"+Guid.NewGuid().ToString("N"));
        try
        {
            var unavailableMonth=Directory.CreateDirectory(Path.Combine(root,"2026-10")).FullName;
            var availableMonth=Directory.CreateDirectory(Path.Combine(root,"2026-11")).FullName;
            var unavailable=Path.Combine(unavailableMonth,"app_20261001_010101.png");var available=Path.Combine(availableMonth,"app_20261101_010101.png");
            await File.WriteAllTextAsync(unavailable,"old");await File.WriteAllTextAsync(available,"new");
            var history=new CaptureHistory(()=>new AppSettings(HistoryDirectory:root),path=>
            {
                if(path==(missingMonth?unavailableMonth:unavailable))throw new FileNotFoundException("Removed during scan");
                return File.GetAttributes(path);
            });
            var entries=await history.ListEntriesAsync();var item=Assert.Single(entries);Assert.Equal(available,item.Path);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task CancellationInsideFilesystemScanIsNotSwallowedAsUnavailableMedia()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-cancel-"+Guid.NewGuid().ToString("N"));
        try
        {
            var month=Directory.CreateDirectory(Path.Combine(root,"2026-11")).FullName;
            await File.WriteAllTextAsync(Path.Combine(month,"app_20261101_010101.png"),"fixture");
            using var cancellation=new CancellationTokenSource();
            var history=new CaptureHistory(()=>new AppSettings(HistoryDirectory:root),path=>{cancellation.Cancel();cancellation.Token.ThrowIfCancellationRequested();return File.GetAttributes(path);});
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>history.ListEntriesAsync(cancellation.Token));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
