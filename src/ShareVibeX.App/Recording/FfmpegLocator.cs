namespace ShareVibeX.App.Recording;

public static class FfmpegLocator
{
    public static string Resolve(string? configuredPath, string appDirectory, string? searchPath = null,
        Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        appDirectory = Path.GetFullPath(appDirectory);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath.Trim()), appDirectory);
            if (!exists(path)) throw new FileNotFoundException(Loc.T("The configured FFmpeg executable was not found. Correct its path or leave it empty for automatic discovery."), path);
            return path;
        }
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var candidates = new List<string>();
        foreach (var directory in (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try { candidates.Add(Path.GetFullPath(Path.Combine(directory.Trim().Trim('"'), name))); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException) { }
        }
        if (OperatingSystem.IsWindows())
        {
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                if (!string.IsNullOrEmpty(root)) candidates.Add(Path.Combine(root, "ffmpeg", "bin", name));
        }
        candidates.Add(Path.Combine(appDirectory, "tools", name));
        candidates.Add(Path.Combine(appDirectory, "tools", "ffmpeg", "bin", name));
        return candidates.FirstOrDefault(exists) ?? throw new FileNotFoundException(Loc.T("FFmpeg was not found. Install it separately, select its executable, or put it in tools/ffmpeg/bin beside ShareVibeX."));
    }
}
