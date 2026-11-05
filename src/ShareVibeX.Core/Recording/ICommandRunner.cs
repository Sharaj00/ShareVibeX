namespace ShareVibeX.Core.Recording;

public sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

public interface ICommandRunner
{
    Task<ToolResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token);
}
