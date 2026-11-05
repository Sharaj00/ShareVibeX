using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ShareVibeX.App.Capture;
using ShareVibeX.App.Editing;

namespace ShareVibeX.App.Tests;

public sealed class OverlayToolbarLayoutTests
{
    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void RightHandRegionKeepsUsefulPanelBesideRegion(double scale)
    {
        var viewport = new Size(1171 / scale, 900 / scale);
        var region = new Rect(643 / scale, 0, 528 / scale, 599 / scale);
        var width = OverlayToolbarLayout.EditorWidth(528, scale, viewport.Width);
        var position = OverlayToolbarLayout.Place(region, new Size(width, 90), viewport);
        Assert.Equal(Math.Min(734,viewport.Width-24),width);
        Assert.Equal(width,OverlayToolbarLayout.EditorWidth(80,scale,viewport.Width));
        Assert.True(position.X + width <= viewport.Width);
        Assert.True(position.Y + 90 <= viewport.Height);
    }
    [Theory]
    [InlineData(110)]
    [InlineData(240)]
    [InlineData(690)]
    public void PanelsOfDifferentWidthsShareTheRegionCenter(int width)
    {
        var region=new Rect(440,120,160,320);
        var panel=OverlayToolbarLayout.Place(region,new Size(width,70),new Size(1200,800));
        Assert.Equal(region.Center.X,panel.X+width/2d);
    }

    [Fact]
    public void BottomRegionPlacesPanelAboveWithoutLeavingMonitor()
    {
        var point = OverlayToolbarLayout.Place(new(900, 700, 300, 300), new(400, 120), new(1280, 1024));
        Assert.True(point.Y + 120 < 700); Assert.InRange(point.X, 0, 880);
    }
}

[Collection("Avalonia UI")]
public sealed class EditorExportIconTests(UiTestFixture ui)
{
    [Fact]
    public Task CopyAndSaveAreAccessibleIcons() => ui.Run(() =>
    {
        var toolbar = new EditorToolbar(new(), () => { }, _ => Task.CompletedTask, () => { });
        foreach (var name in new[] { "Copy screenshot", "Save screenshot" })
        {
            var button = toolbar.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b => AutomationProperties.GetName(b) == name);
            Assert.NotNull(button); Assert.IsType<PathIcon>(button.Content);Assert.Equal(40,button.Width);Assert.Equal(40,button.Height); Assert.NotNull(ToolTip.GetTip(button));
        }
    });
}
