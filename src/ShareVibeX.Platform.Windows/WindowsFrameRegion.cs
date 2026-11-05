using System.Runtime.InteropServices;
namespace ShareVibeX.Platform.Windows;
public static class WindowsFrameRegion
{
    public static bool Apply(nint window,int width,int height,int thickness=2)
    {
        if(!OperatingSystem.IsWindows()||window==0||width<3||height<3)return false;
        var outer=CreateRectRgn(0,0,width,height);var inner=CreateRectRgn(thickness,thickness,width-thickness,height-thickness);
        try
        {
            if(outer==0||inner==0||CombineRgn(outer,outer,inner,4)==0)return false;
            if(SetWindowRgn(window,outer,true)==0)return false;
            outer=0;return true;
        }
        finally{if(inner!=0)DeleteObject(inner);if(outer!=0)DeleteObject(outer);}
    }
    [DllImport("gdi32.dll")]private static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")]private static extern int CombineRgn(nint destination,nint first,nint second,int mode);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint region);
    [DllImport("user32.dll")]private static extern int SetWindowRgn(nint window,nint region,bool redraw);
}
