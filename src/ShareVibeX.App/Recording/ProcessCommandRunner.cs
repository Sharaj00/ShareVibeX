using System.Diagnostics;
using System.Text;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.App.Recording;

public sealed class ProcessCommandRunner(TimeSpan? timeout = null, int outputLimit = 1_048_576) : ICommandRunner
{
    public async Task<ToolResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var timer = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(8));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, timer.Token);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException(Loc.T("The FFmpeg process could not be started."));
        process.StandardInput.Close();
        try
        {
            var stdout = ReadAsync(process.StandardOutput);
            var stderr = ReadAsync(process.StandardError);
            await Task.WhenAll(process.WaitForExitAsync(cancellation.Token), stdout, stderr);
            return new(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && timer.IsCancellationRequested)
        { throw new TimeoutException(Loc.T("FFmpeg did not finish its capability test in time.")); }
        finally
        {
            if (!process.HasExited)
            {
                Kill();
                // Wait for termination so no probe survives a closed settings window.
                await process.WaitForExitAsync();
            }
        }

        async Task<string> ReadAsync(StreamReader reader)
        {
            try { return await ReadBoundedAsync(reader, cancellation.Token); }
            catch (IOException) { Kill(); throw; }
        }

        void Kill()
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }

    private async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder(); var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, token)) > 0)
        {
            if (result.Length + count > outputLimit) throw new IOException(Loc.T("FFmpeg capability output exceeded the allowed size."));
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
