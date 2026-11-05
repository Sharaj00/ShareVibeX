using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShareVibeX.App.Capture;
using ShareVibeX.Core.Capture;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.Windows.Smoke;

internal static class Program
{
    public static string Output = "";
    public static int Result = 1;
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows interactive desktop required."); return 2; }
        Output = Path.GetFullPath(args.Length > 0 ? args[0] : "TestResults/windows-smoke");
        AppBuilder.Configure<SmokeApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        return Result;
    }
}

internal sealed class SmokeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var color = Color.FromRgb(23,137,211);
        var window = new Window { Title = "ShareVibeX Windows verification", Width = 360, Height = 240,
            Background = new SolidColorBrush(color), WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
        lifetime.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                await Task.Delay(400);
                var desktop = new WindowsDesktopCapture();
                var snapshot = await desktop.CaptureAsync();
                GetCursorPos(out var cursor);
                // Keep the color probe away from pointer-highlight software on the interactive desktop.
                var p = new[] { new Point(30,60),new Point(280,160),new Point(180,100) }
                    .Select(window.PointToScreen).MaxBy(point => (long)(point.X-cursor.X)*(point.X-cursor.X)+(long)(point.Y-cursor.Y)*(point.Y-cursor.Y));
                Console.WriteLine($"Probe {p}; pointer {cursor.X},{cursor.Y}; scale {window.RenderScaling}.");
                var service = new BitmapExportService();
                var frame = service.Crop(snapshot,new ShareVibeX.Core.Capture.PixelRect(p.X,p.Y,20,20));
                Directory.CreateDirectory(Program.Output);
                await service.SavePngAsync(frame,Path.Combine(Program.Output,"probe.png"));
                for (var i=0;i<frame.Bgra32.Length;i+=4)
                    Require(frame.Bgra32[i]==211 && frame.Bgra32[i+1]==137 && frame.Bgra32[i+2]==23 && frame.Bgra32[i+3]==255,
                        $"Actual desktop capture pixel mismatch at {i/4}: BGRA={frame.Bgra32[i]},{frame.Bgra32[i+1]},{frame.Bgra32[i+2]},{frame.Bgra32[i+3]}; probe={p}; scale={window.RenderScaling}.");
                Directory.CreateDirectory(Program.Output);
                var path = Path.Combine(Program.Output,"日本語 capture check.png");
                await service.SavePngAsync(frame,path);
                using (var decoded = new Bitmap(path)) Require(decoded.PixelSize == new PixelSize(20,20),"PNG dimensions mismatch.");
                using var owner = new ClipboardImageOwner();
                await owner.CopyAsync(window,frame);
                using var pasted = await window.Clipboard!.TryGetBitmapAsync();
                Require(pasted is not null && pasted.PixelSize==new PixelSize(20,20),"Windows clipboard image missing.");
                var memory = Marshal.AllocHGlobal(1600);
                try
                {
                    pasted!.CopyPixels(new Avalonia.PixelRect(0,0,20,20),memory,1600,80);
                    var bytes = new byte[1600]; Marshal.Copy(memory,bytes,0,1600);
                    var rgba = pasted.Format == PixelFormat.Rgba8888;
                    Require(bytes[rgba?0:2]==23 && bytes[1]==137 && bytes[rgba?2:0]==211 && bytes[3]==255,"Clipboard pixels differ.");
                }
                finally { Marshal.FreeHGlobal(memory); }
                using var first = new WindowsGlobalHotkey();
                using var second = new WindowsGlobalHotkey();
                first.Register("Ctrl+Alt+Shift+F11",() => { });
                var conflict = false;
                try { second.Register("Ctrl+Alt+Shift+F11",() => { }); }
                catch (System.ComponentModel.Win32Exception) { conflict = true; }
                Require(conflict,"Duplicate shortcut was not rejected.");
                first.Unregister(); second.Register("Ctrl+Alt+Shift+F11",() => { });
                Console.WriteLine($"PASS: {snapshot.Monitors.Count} monitor(s), exact BGRA desktop pixels, Unicode PNG save/decode, real Windows clipboard round-trip, hotkey conflict/recovery.");
                Program.Result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); Program.Result = 1; }
            finally { lifetime.Shutdown(); }
        };
        base.OnFrameworkInitializationCompleted();
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct CursorPosition { public int X,Y; }
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPosition position);
}
