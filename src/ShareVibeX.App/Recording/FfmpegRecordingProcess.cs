using System.Diagnostics;
using System.Globalization;
using System.Text;
using ShareVibeX.Core.Recording;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Recording;

public sealed class FfmpegRecordingProcessFactory(Action<Process>? terminate=null):IRecordingProcessFactory
{
    public IRecordingProcess Start(string executable,IReadOnlyList<string> arguments)=>new FfmpegRecordingProcess(executable,arguments,terminate);
}

internal sealed class FfmpegRecordingProcess:IRecordingProcess
{
    private readonly Process process;
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StringBuilder diagnostics=new();
    private readonly Task stdout,stderr;
    private int stop;
    private readonly WindowsChildProcessJob? job;
    private readonly Action<Process>? terminate;
    private RecordingProgress progressSnapshot=RecordingProgress.Empty;
    public RecordingProgress Progress=>Volatile.Read(ref progressSnapshot);
    public Task Ready=>ready.Task;
    public Task<int> Exit{get;}
    public string Diagnostics{get{lock(diagnostics)return diagnostics.ToString();}}
    public FfmpegRecordingProcess(string executable,IReadOnlyList<string> arguments,Action<Process>? terminate=null)
    {
        this.terminate=terminate;
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in arguments)start.ArgumentList.Add(argument);
        process=new Process{StartInfo=start};
        try
        {
            if(OperatingSystem.IsWindows())job=new WindowsChildProcessJob();
            if(!process.Start())throw new IOException(Loc.T("Could not start FFmpeg."));
            job?.Assign(process.Handle);
        }
        catch
        {
            try{if(process.Id!=0 && !process.HasExited)process.Kill(true);}catch(InvalidOperationException){ }
            job?.Dispose();process.Dispose();throw;
        }
        stdout=ReadAsync(process.StandardOutput,true);stderr=ReadAsync(process.StandardError,false);Exit=ObserveAsync();
    }
    private async Task ReadAsync(StreamReader reader,bool progress)
    {
        var buffer=new char[2048];var pending=new StringBuilder();int count;
        TimeSpan? duration=null;long? bytes=null;long? framesReported=null;
        while((count=await reader.ReadAsync(buffer).ConfigureAwait(false))>0)
        {
            for(var i=0;i<count;i++)
            {
                if(buffer[i]=='\n'){Line(pending.ToString());pending.Clear();}
                else if(buffer[i]!='\r' && pending.Length<4096)pending.Append(buffer[i]);
            }
        }
        if(pending.Length>0)Line(pending.ToString());
        void Line(string line)
        {
            if(progress)
            {
                var separator=line.IndexOf('=');
                if(separator>0)
                {
                    var key=line[..separator];var value=line.AsSpan(separator+1);
                    if(long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var number)&&number>=0)
                    {
                        if(key=="out_time_us" && number<=long.MaxValue/10)duration=TimeSpan.FromTicks(number*10);
                        if(key=="total_size")bytes=number;
                        if(key=="frame")framesReported=number;
                    }
                    if(key=="progress")Volatile.Write(ref progressSnapshot,new(duration,bytes,framesReported));
                }
            }
            if(progress && line.StartsWith("frame=",StringComparison.Ordinal) && int.TryParse(line.AsSpan(6),NumberStyles.Integer,CultureInfo.InvariantCulture,out var frames)&&frames>0)
                ready.TrySetResult();
            if(!progress)
            {
                lock(diagnostics)
                {
                    diagnostics.AppendLine(line.Length>4096?line[^4096..]:line);
                    if(diagnostics.Length>32768)diagnostics.Remove(0,diagnostics.Length-32768);
                }
            }
        }
    }
    private async Task<int> ObserveAsync()
    {
        try{await process.WaitForExitAsync().ConfigureAwait(false);await Task.WhenAll(stdout,stderr).ConfigureAwait(false);return process.ExitCode;}
        finally{if(!ready.Task.IsCompleted)ready.TrySetException(new IOException(Loc.T("FFmpeg exited before reporting a video frame. ")+Diagnostics));}
    }
    public async Task RequestStopAsync()
    {
        if(Interlocked.Exchange(ref stop,1)!=0 || process.HasExited)return;
        try{await process.StandardInput.WriteLineAsync("q");await process.StandardInput.FlushAsync();}
        catch(IOException)when(process.HasExited){ }
    }
    public void Kill(){try{if(!process.HasExited){if(terminate is null)process.Kill(true);else terminate(process);}}catch(InvalidOperationException){ }}
    public async ValueTask DisposeAsync()
    {
        try{Kill();await Exit.WaitAsync(TimeSpan.FromSeconds(3));}
        finally{try{job?.Dispose();}finally{process.Dispose();}}
    }
}
