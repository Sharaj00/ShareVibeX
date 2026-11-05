namespace ShareVibeX.App;

internal static class DiagnosticLog
{
    private static readonly object Gate = new();
    public static string? DirectoryPath { get; set; }
    public static void Write(string message)
    {
        if (DirectoryPath is not { } directory) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory,"diagnostic.log");
                if (File.Exists(path) && new FileInfo(path).Length > 128*1024) File.Delete(path);
                File.AppendAllText(path,$"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch { /* Diagnostics must never prevent capture or settings recovery. */ }
    }
}
