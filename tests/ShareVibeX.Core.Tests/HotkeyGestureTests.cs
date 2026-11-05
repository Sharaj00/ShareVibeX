using ShareVibeX.Core.Platform;

namespace ShareVibeX.Core.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+PrintScreen",2u,44u)]
    [InlineData("PrintScreen",0u,44u)]
    [InlineData("F8",0u,119u)]
    [InlineData("alt+shift+F12",5u,123u)]
    [InlineData("Win+S",8u,83u)]
    [InlineData("Ctrl+Alt+9",3u,57u)]
    public void ParsesSupportedGestures(string text, uint modifiers, uint key) => Assert.Equal(new HotkeyGesture(modifiers,key), HotkeyGesture.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("9")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+S")]
    [InlineData("Ctrl+F13")]
    [InlineData("Hyper+S")]
    public void RejectsInvalidOrUnmodifiedGestures(string text) => Assert.Throws<FormatException>(() => HotkeyGesture.Parse(text));
}
