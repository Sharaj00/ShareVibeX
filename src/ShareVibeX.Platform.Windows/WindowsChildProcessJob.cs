using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ShareVibeX.Platform.Windows;

// Closing the parent's job handle also ends the recorder after an unexpected application exit.
public sealed class WindowsChildProcessJob:IDisposable
{
    private readonly SafeFileHandle handle;
    public WindowsChildProcessJob()
    {
        handle=CreateJobObject(0,null);
        if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits=new ExtendedLimits{Basic=new BasicLimits{Flags=0x2000}}; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>()))
        {var error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
    }
    public void Assign(nint process)
    {if(!AssignProcessToJobObject(handle,process))throw new Win32Exception(Marshal.GetLastWin32Error(),Loc.T("Could not bind recorder lifetime to ShareVibeX."));}
    public void Dispose()=>handle.Dispose();
    [StructLayout(LayoutKind.Sequential)]private struct BasicLimits
    {public long ProcessTime,JobTime;public uint Flags;public nuint MinWorkingSet,MaxWorkingSet;public uint ActiveProcesses;public nuint Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)]private struct IoCounters
    {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)]private struct ExtendedLimits
    {public BasicLimits Basic;public IoCounters Io;public nuint ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateJobObject(nint attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref ExtendedLimits limits,uint length);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AssignProcessToJobObject(SafeFileHandle job,nint process);
}
