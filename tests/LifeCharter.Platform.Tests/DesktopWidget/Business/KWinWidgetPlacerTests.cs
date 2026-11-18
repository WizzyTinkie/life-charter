using LifeCharter.Platform.DesktopWidget.Business;
using LifeCharter.Platform.DesktopWidget.Models;

namespace LifeCharter.Platform.Tests.DesktopWidget.Business;

public sealed class KWinWidgetPlacerTests
{
    [Fact]
    public void Script_targets_the_widget_app_id_and_uses_the_work_area()
    {
        var script = KWinWidgetPlacer.BuildCornerScript("lifecharter-widget", WidgetCorner.TopRight, 24);

        Assert.Contains("w.resourceClass !== \"lifecharter-widget\"", script);
        Assert.Contains("workspace.clientArea(KWin.PlacementArea, w)", script);
        Assert.Contains("x: false ? area.x + 24 : area.x + area.width - c.width - 24", script);
        Assert.Contains("y: true ? area.y + 24 : area.y + area.height - c.height - 24", script);
    }

    [Fact]
    public void Saved_position_is_clamped_to_the_work_area()
    {
        var script = KWinWidgetPlacer.BuildPositionScript("lifecharter-widget", new WidgetPosition(1234.5, 99));

        Assert.Contains("x: Math.min(Math.max(1234.5, area.x), area.x + area.width - c.width)", script);
        Assert.Contains("y: Math.min(Math.max(99, area.y), area.y + area.height - c.height)", script);
    }

    [Theory]
    [InlineData(WidgetCorner.TopLeft, "x: true", "y: true")]
    [InlineData(WidgetCorner.BottomLeft, "x: true", "y: false")]
    [InlineData(WidgetCorner.BottomRight, "x: false", "y: false")]
    public void Corner_decides_the_edges(WidgetCorner corner, string horizontal, string vertical)
    {
        var script = KWinWidgetPlacer.BuildCornerScript("lifecharter-widget", corner, 10);

        Assert.Contains(horizontal + " ?", script);
        Assert.Contains(vertical + " ?", script);
    }
}
