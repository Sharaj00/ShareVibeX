using ShareVibeX.App.Recording;

namespace ShareVibeX.App.Tests;

public sealed class ProcessCommandRunnerTests
{
    private static string Shell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
    [Fact]
    public async Task ArgumentsStayLiteralAndBothOutputStreamsAreRead()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await new ProcessCommandRunner().RunAsync(Shell,
            ["-NoProfile", "-NonInteractive", "-Command", "[Console]::Out.Write('output with spaces'); [Console]::Error.Write('diagnostic'); exit 7"], CancellationToken.None);
        Assert.Equal(7, result.ExitCode); Assert.Equal("output with spaces", result.StandardOutput); Assert.Equal("diagnostic", result.StandardError);
    }
    [Fact]
    public async Task HungChildIsTerminatedOnTimeout()
    {
        if (!OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<TimeoutException>(() => new ProcessCommandRunner(TimeSpan.FromMilliseconds(800)).RunAsync(Shell,
            ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"], CancellationToken.None));
    }
    [Fact]
    public async Task OversizedOutputIsReportedAsAnOutputError()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var cancel=new CancellationTokenSource();
        try
        {
            await Assert.ThrowsAsync<IOException>(() => new ProcessCommandRunner(TimeSpan.FromSeconds(15), 1024).RunAsync(Shell,
                ["-NoProfile", "-NonInteractive", "-Command", "[Console]::Out.Write(('x' * 5000)); Start-Sleep -Seconds 30"], cancel.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { cancel.Cancel(); }
    }
}
