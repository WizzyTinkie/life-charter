using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LifeCharter.App.Shell.Business;

/// <summary>Draws the tray icon at runtime: three attribute dots on a dark disc, with a red badge while attacks wait in the evening.</summary>
public static class TrayIconFactory
{
    private const int Size = 64;

    public static WindowIcon Create(bool alert = false)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            var center = new Point(Size / 2.0, Size / 2.0);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#181A20")), new Pen(new SolidColorBrush(Color.Parse("#E8C36A")), 4), center, 28, 28);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#3E8EF7")), null, new Point(32, 18), 8, 8);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#4CAF50")), null, new Point(44, 40), 8, 8);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#E5484D")), null, new Point(20, 40), 8, 8);
            if (alert)
                context.DrawEllipse(new SolidColorBrush(Color.Parse("#FF3B3F")), new Pen(new SolidColorBrush(Color.Parse("#181A20")), 3), new Point(51, 13), 11, 11);
        }

        return new WindowIcon(bitmap);
    }
}
