using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;

public sealed class TrayIconFormatTests
{
    [Fact] public void IcoDirectoryIsUnwrappedBeforeNativeIconCreation()
    {
        var data=new byte[26];data[2]=1;data[4]=1;data[6]=32;data[7]=32;data[14]=4;data[18]=22;
        data[22]=1;data[23]=2;data[24]=3;data[25]=4;
        Assert.Equal(new byte[]{1,2,3,4},WindowsTrayIcon.ReadIconImage(data));
        data[18]=25;Assert.Throws<InvalidDataException>(()=>WindowsTrayIcon.ReadIconImage(data));
    }
}
