using ShareVibeX.Core.Settings;
using ShareVibeX.App.History;

namespace ShareVibeX.App.Tests;

public sealed class CaptureHistoryTests
{
    [Fact]
    public async Task IndependentHistoryWritersKeepBothCapturesWithInvariantDates()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-"+Guid.NewGuid().ToString("N"));
        var previous=System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture=new("th-TH");
        try
        {
            var settings=new AppSettings(HistoryEnabled:true,HistoryDirectory:root);
            var context=new CaptureHistoryContext("Editor",new DateTime(2026,11,5,12,30,0));
            var bothReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var remaining=2;
            Task<string?> Store(string data)=>new CaptureHistory(()=>settings).StoreAsync(context,"png",async (path,token)=>
            {
                await File.WriteAllTextAsync(path,data,token);
                if(Interlocked.Decrement(ref remaining)==0)bothReady.SetResult();
                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
            });
            var files=await Task.WhenAll(Store("first"),Store("second"));
            Assert.Equal(2,files.Distinct().Count());
            Assert.All(files,path=>Assert.StartsWith("Editor_20261105_123000",Path.GetFileName(path)));
            Assert.Equal(new[]{"first","second"},await Task.WhenAll(files.Select(path=>File.ReadAllTextAsync(path!))));
            Assert.Equal(2,Directory.GetFiles(Path.Combine(root,"2026-11")).Length);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture=previous;
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveUsesConfiguredFolderAndTimestampWithoutOverwriting(bool monthFolder)
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-"+Guid.NewGuid().ToString("N"));
        try
        {
            var history=new CaptureHistory(()=>new AppSettings(HistoryEnabled:true,OutputDirectory:root,CreateMonthFolder:monthFolder));
            var context=new CaptureHistoryContext("editor: app",new DateTime(2026,11,3,12,34,56));
            var first=await history.StoreAsync(context,"png",(path,ct)=>File.WriteAllTextAsync(path,"first",ct));
            var second=await history.StoreAsync(context,"png",(path,ct)=>File.WriteAllTextAsync(path,"second",ct));
            Assert.Equal("editor_ app_20261103_123456.png",Path.GetFileName(first));
            Assert.Equal(monthFolder?Path.Combine(root,"2026-11"):root,Path.GetDirectoryName(first));
            Assert.Equal(first,await history.StoreFileAsync(context,first!));
            Assert.NotEqual(first,second);Assert.Equal("first",await File.ReadAllTextAsync(first!));
            Assert.Equal(2,history.List().Count);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task DisabledHistoryDoesNotCreateFiles()
    {
        var history=new CaptureHistory(()=>new AppSettings(HistoryEnabled:false));
        Assert.Null(await history.StoreAsync(new("app",DateTime.Now),"png",(_,_)=>throw new Exception("Must not run")));
    }
    [Fact]
    public async Task FailedWriteLeavesNoHistoryOrTemporaryFile()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-history-"+Guid.NewGuid().ToString("N"));
        try
        {
            var history=new CaptureHistory(()=>new AppSettings(HistoryEnabled:true,HistoryDirectory:root));
            await Assert.ThrowsAsync<IOException>(()=>history.StoreAsync(new("app",DateTime.Now),"mp4",async(path,ct)=>{await File.WriteAllTextAsync(path,"partial",ct);throw new IOException("disk error");}));
            Assert.Empty(Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task HistorySettingsSurvivePortableRoundTrip()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-settings-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new PortableSettingsStore(root);
            var settings=new AppSettings(HistoryEnabled:true,HistoryDirectory:Path.Combine(root,"captures"));
            await store.SaveAsync(settings);var restored=await store.LoadAsync();
            Assert.True(restored.HistoryEnabled);Assert.Equal(settings.HistoryDirectory,restored.HistoryDirectory);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
