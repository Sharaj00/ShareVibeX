using System.Buffers.Binary;
using System.Text;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;
public sealed class ClipboardFileTests
{
    [Fact]
    public void FileDropUsesUnicodeAbsolutePathAndDoubleTerminator()
    {
        var path=Path.GetFullPath("video 日本語.mp4");var bytes=WindowsFileClipboard.EncodeFileDrop(path);
        Assert.Equal(20,BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(1,BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(path+"\0\0",Encoding.Unicode.GetString(bytes,20,bytes.Length-20));
        Assert.Throws<ArgumentException>(()=>WindowsFileClipboard.EncodeFileDrop("relative.mp4"));
    }
}
