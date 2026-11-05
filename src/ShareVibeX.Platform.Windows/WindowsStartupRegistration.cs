using Microsoft.Win32;

namespace ShareVibeX.Platform.Windows;

public sealed class WindowsStartupRegistration
{
    private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName="ShareVibeX";
    private readonly string executable;private readonly Func<string?> read;private readonly Action<string?> write;
    public WindowsStartupRegistration(string executable,Func<string?>? read=null,Action<string?>? write=null)
    {
        this.executable=executable;
        if(string.IsNullOrWhiteSpace(executable)||executable.Contains('"'))throw new ArgumentException(Loc.T("Invalid application executable."));
        this.read=read??ReadRegistry;this.write=write??WriteRegistry;
    }
    public string? Read()=>read();
    public bool IsEnabled=>string.Equals(read(),Command,StringComparison.OrdinalIgnoreCase);
    private string Command=>$"\"{executable}\"";
    public void Restore(string? previous)=>write(previous);
    public void SetEnabled(bool enabled)
    {
        var existing=read();
        if(existing is not null&&!IsOwned(existing))throw new InvalidOperationException(Loc.T("The ShareVibeX startup entry belongs to another command. Remove the conflicting entry manually before changing autostart."));
        var desired=enabled?Command:null;if(!string.Equals(existing,desired,StringComparison.Ordinal))write(desired);
    }
    private static bool IsOwned(string command)
    {
        // Accept only our exact executable without arbitrary command-line arguments.
        var path=command.StartsWith('"')&&command.EndsWith('"')?command[1..^1]:command;
        return !path.Contains('"')&&string.Equals(Path.GetFileName(path),"ShareVibeX.exe",StringComparison.OrdinalIgnoreCase);
    }
    private static string? ReadRegistry()
    {
        if(!OperatingSystem.IsWindows())return null;
        using var key=Registry.CurrentUser.OpenSubKey(KeyPath,false);var value=key?.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
        if(value is not null&&value is not string)throw new InvalidOperationException(Loc.T("The ShareVibeX startup entry has an unsupported value type."));
        return value as string;
    }
    private static void WriteRegistry(string? value)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(Loc.T("Autostart is currently available on Windows only."));
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath,true);
        if(value is null)key.DeleteValue(ValueName,false);else key.SetValue(ValueName,value,RegistryValueKind.String);
    }
}
