namespace ShareVibeX.Core.IO;

public static class PortablePaths
{
    public static string BaseDirectory {get;}=Resolve(AppContext.BaseDirectory,Environment.ProcessPath);

    private static string Resolve(string assemblyDirectory,string? processPath)
    {
        var directory=Path.TrimEndingDirectorySeparator(assemblyDirectory);
        var processDirectory=Path.GetDirectoryName(processPath);
        return processDirectory is not null&&string.Equals(directory,Path.Combine(processDirectory,"libs"),StringComparison.OrdinalIgnoreCase)
            ?processDirectory:assemblyDirectory;
    }
}
