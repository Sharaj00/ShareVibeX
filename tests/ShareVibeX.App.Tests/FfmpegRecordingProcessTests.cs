using ShareVibeX.App.Recording;
using ShareVibeX.Platform.Windows;
using System.Diagnostics;

namespace ShareVibeX.App.Tests;

public sealed class FfmpegRecordingProcessTests
{
    [Fact]
    public async Task FailedKillStillClosesWindowsJobAndEndsItsConsoleChild()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        Process? observer=null;
        var process=new FfmpegRecordingProcessFactory(child=>{observer=Process.GetProcessById(child.Id);throw new System.ComponentModel.Win32Exception("fixture denial");})
            .Start(shell,["-NoProfile","-NonInteractive","-Command","[Console]::Out.WriteLine('frame=1'); [Console]::Out.Flush(); Start-Sleep -Seconds 30"]);
        try
        {
            await process.Ready.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(()=>process.DisposeAsync().AsTask());
            Assert.NotNull(observer);await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally{if(observer is not null){if(!observer.HasExited)observer.Kill(true);observer.Dispose();}}
    }
    [Fact]
    public async Task FragmentedProgressPublishesMetricsAndIgnoresInvalidValues()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        await using var process=new FfmpegRecordingProcessFactory().Start(shell,["-NoProfile","-NonInteractive","-Command",
            "[Console]::Out.Write('out_time_'); [Console]::Out.Flush(); [Console]::Out.WriteLine('us=2000000'); [Console]::Out.WriteLine('frame=60'); [Console]::Out.WriteLine('total_size=1000000'); [Console]::Out.WriteLine('progress=continue'); [Console]::Out.WriteLine('out_time_us=N/A'); [Console]::Out.WriteLine('total_size=-1'); [Console]::Out.WriteLine('progress=end'); exit 0"]);
        Assert.Equal(0,await process.Exit.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(2),process.Progress.Duration);
        Assert.Equal(1000000,process.Progress.TotalBytes);
        Assert.Equal(4000,process.Progress.AverageBitrateKbps);
        Assert.Equal(60,process.Progress.Frames);
    }
    [Fact]
    public async Task InvalidOrOverflowingProgressDoesNotInventMetrics()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        await using var process=new FfmpegRecordingProcessFactory().Start(shell,["-NoProfile","-NonInteractive","-Command",
            "[Console]::Out.WriteLine('out_time_us=9223372036854775807'); [Console]::Out.WriteLine('total_size=not a number'); [Console]::Out.WriteLine('progress=end'); exit 0"]);
        await process.Exit.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(process.Progress.Duration);Assert.Null(process.Progress.TotalBytes);Assert.Null(process.Progress.AverageBitrateKbps);
    }
    [Fact]
    public async Task ClosingWindowsJobEndsItsConsoleChild()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        var start=new ProcessStartInfo(shell){UseShellExecute=false,CreateNoWindow=true};
        foreach(var argument in new[]{"-NoProfile","-NonInteractive","-Command","Start-Sleep -Seconds 30"})start.ArgumentList.Add(argument);
        using var child=Process.Start(start)!;
        try
        {
            using(var job=new WindowsChildProcessJob()){job.Assign(child.Handle);}
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.True(child.HasExited);
        }
        finally{if(!child.HasExited)child.Kill(true);}
    }
    [Fact]
    public async Task ProgressAndGracefulStopWorkThroughHiddenConsoleChild()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        await using var process=new FfmpegRecordingProcessFactory().Start(shell,["-NoProfile","-NonInteractive","-Command",
            "[Console]::Out.WriteLine('frame=1'); [Console]::Out.Flush(); $line=[Console]::ReadLine(); if ($line -eq 'q') { exit 0 }; exit 9"]);
        await process.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        await process.RequestStopAsync();Assert.Equal(0,await process.Exit.WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Fact]
    public async Task ExitBeforeFirstFrameReportsBoundedDiagnostics()
    {
        if(!OperatingSystem.IsWindows())return;
        var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        await using var process=new FfmpegRecordingProcessFactory().Start(shell,["-NoProfile","-NonInteractive","-Command",
            "[Console]::Error.WriteLine(('bad encoder ' * 8000)); exit 1"]);
        await Assert.ThrowsAsync<IOException>(()=>process.Ready.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1,await process.Exit);Assert.InRange(process.Diagnostics.Length,1,32768);
    }
}
