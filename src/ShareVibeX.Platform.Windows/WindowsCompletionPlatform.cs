using System.Runtime.InteropServices;
namespace ShareVibeX.Platform.Windows;

public static class WindowsCompletionPlatform
{
    private static readonly object SoundGate=new();
    private static bool soundOwned;
    public static void PlayCompletionSound()=>PlayCompletionSound(null);
    public static void PlayCompletionSound(string? customSoundPath)
    {
        if(!OperatingSystem.IsWindows())return;
        lock(SoundGate)
        {
            StopCompletionSound();
            try
            {
                var path=CompletionSoundFile.TryResolve(customSoundPath,ShareVibeX.Core.IO.PortablePaths.BaseDirectory);
                if(path is not null&&PlaySound(path,0,0x20000|0x1|0x2)){soundOwned=true;return;}
                // Use our own async alias playback so Block can stop even the default sound.
                soundOwned=PlaySound("SystemAsterisk",0,0x10000|0x1|0x2);
            }
            catch{soundOwned=false;}
        }
    }
    public static void StopCompletionSound()
    {
        if(!OperatingSystem.IsWindows())return;
        lock(SoundGate)
        {
            if(!soundOwned)return;soundOwned=false;
            try{PlaySound(null,0,0);}catch{ }
        }
    }
    public static bool IsExclusiveFullscreenForeground()
    {
        if(!OperatingSystem.IsWindows())return false;
        return IsExclusiveFullscreenState(SHQueryUserNotificationState(out var state),state);
    }
    public static bool IsExclusiveFullscreenState(int result,int state)=>result>=0&&state==3;
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    [DllImport("winmm.dll",CharSet=CharSet.Unicode,EntryPoint="PlaySoundW")] private static extern bool PlaySound(string? sound,nint module,uint flags);
}
