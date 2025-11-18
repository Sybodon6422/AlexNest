using System.Windows;
using System.Windows.Media;
using AlexNest.Core.Algorithms;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

    public class NestViewer : FrameworkElement
{
    public NestPlate? Plate { get; set; }
    public NestingResult? Result { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (Plate == null)
        {
            DrawTextCentered(dc, "No plate loaded");
            return;
        }

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Compute scale to fit plate into view with a small margin
        double margin = 20;
        double scaleX = (w - 2 * margin) / Plate.Width;
        double scaleY = (h - 2 * margin) / Plate.Height;
        double scale = Math.Min(scaleX, scaleY);

        // Plate origin in screen space (we’ll put 0,0 at bottom-left)
        Point origin = new Point(margin, h - margin);

        // Draw plate outline
        var plateRect = new Rect(
            origin.X,
            origin.Y - Plate.Height * scale,
            Plate.Width * scale,
            Plate.Height * scale);

        var platePen = new Pen(Brushes.LightGray, 1);
        dc.DrawRectangle(Brushes.White, platePen, plateRect);

        if (Result == null || Result.Placements.Count == 0)
        {
            DrawTextCentered(dc, "No placements. Load parts + plate and click Run Nest.");
            return;
        }

        var partPen = new Pen(Brushes.DarkBlue, 1);
        var partFill = new SolidColorBrush(Color.FromArgb(40, 0, 0, 255));

        foreach (var placement in Result.Placements)
        {
            DrawPart(dc, placement, origin, scale, partPen, partFill);
        }
    }

    private void DrawPart(DrawingContext dc, PartPlacement placement, Point origin, double scale,
        Pen outline, Brush fill)
    {
        var part = placement.Part;
        double rotRad = placement.RotationDeg * Math.PI / 180.0;
        var offset = new Vec2(-part.Bounds.MinX, -part.Bounds.MinY);

        foreach (var contour in part.Contours)
        {
            var geom = new StreamGeometry();

            using (var ctx = geom.Open())
            {
                bool first = true;
                for (int i = 0; i < contour.Vertices.Count; i++)
                {
                    var vLocal = contour.Vertices[i];
                    var vRotLocal = Vec2.Rotate(vLocal + offset, rotRad);
                    var vWorld = vRotLocal + placement.Position;

                    // convert to screen coords: origin is bottom-left
                    double sx = origin.X + vWorld.X * scale;
                    double sy = origin.Y - vWorld.Y * scale;

                    if (first)
                    {
                        ctx.BeginFigure(new Point(sx, sy), true, true);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(new Point(sx, sy), true, false);
                    }
                }
            }

            geom.Freeze();
            dc.DrawGeometry(fill, outline, geom);
        }
    }

    private void DrawTextCentered(DrawingContext dc, string text)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            16,
            Brushes.DarkGray,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var loc = new Point(
            (ActualWidth - ft.Width) / 2,
            (ActualHeight - ft.Height) / 2);

        dc.DrawText(ft, loc);
    }
}
