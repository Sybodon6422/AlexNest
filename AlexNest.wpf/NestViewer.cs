using System;
using System.Windows;
using System.Windows.Media;
using AlexNest.Core.Model;
using AlexNest.Core.Geometry;
using AlexNest.Core.Algorithms;

namespace AlexNest.wpf
{
    public class NestViewer : FrameworkElement
    {
        public NestPlate? Plate { get; set; }
        public NestingResult? Result { get; set; }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Plate == null)
            {
                DrawCenteredText(dc, "No plate loaded");
                return;
            }

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Compute uniform scale with margin
            double margin = 20;
            double scaleX = (w - 2 * margin) / Plate.Width;
            double scaleY = (h - 2 * margin) / Plate.Height;
            double scale = Math.Min(scaleX, scaleY);

            // Place world origin at bottom-left of screen
            Point origin = new Point(margin, h - margin);

            DrawPlate(dc, origin, scale);

            if (Result == null || Result.Placements.Count == 0)
            {
                DrawCenteredText(dc, "No placements to display.");
                return;
            }

            var outlinePen = new Pen(Brushes.DarkBlue, 1);
            var fillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 255));

            foreach (var place in Result.Placements)
                DrawPlacement(dc, place, origin, scale, outlinePen, fillBrush);
        }


        // ───────────────────────────────────────────────
        // DRAW PLATE
        // ───────────────────────────────────────────────
        private void DrawPlate(DrawingContext dc, Point origin, double scale)
        {
            var plateRect = new Rect(
                origin.X,
                origin.Y - Plate!.Height * scale,
                Plate.Width * scale,
                Plate.Height * scale);

            var platePen = new Pen(Brushes.LightGray, 1);

            dc.DrawRectangle(Brushes.White, platePen, plateRect);
        }


        // ───────────────────────────────────────────────
        // DRAW PART PLACEMENT
        // ───────────────────────────────────────────────
        private void DrawPlacement(
            DrawingContext dc,
            PartPlacement placement,
            Point origin,
            double scale,
            Pen outline,
            Brush fill)
        {
            var part = placement.Part;

            // radians already in placement (per your PartPlacement ctor)
            double rotRad = placement.RotationDeg;

            var worldTrans = new TranslateTransform(
                placement.Position.X,
                placement.Position.Y);

            ScaleTransform? mirror = null;
            if (placement.Mirrored)
                mirror = new ScaleTransform(-1, 1, 0, 0);

            var rotate = new RotateTransform(rotRad * 180.0 / Math.PI);

            // Group all contours into one geometry so holes subtract
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };

            foreach (var contour in part.Contours)
            {
                var geom = BuildContourGeometry(
                    contour,
                    part.Bounds.MinX,
                    part.Bounds.MinY,
                    mirror,
                    rotate,
                    worldTrans,
                    origin,
                    scale);

                group.Children.Add(geom);
            }

            group.Freeze();

            dc.DrawGeometry(fill, outline, group);
        }



        // ───────────────────────────────────────────────
        // BUILD GEOMETRY FOR ONE CONTOUR
        // Applies:
        //   + Offset to local origin
        //   + Optional mirror
        //   + Rotation
        //   + Translation to world
        //   + Projection to screen coords
        // ───────────────────────────────────────────────
        private Geometry BuildContourGeometry(
            NestContour contour,
            double offsetX,
            double offsetY,
            ScaleTransform? mirror,
            RotateTransform rotate,
            TranslateTransform worldTrans,
            Point origin,
            double scale)
        {
            var sg = new StreamGeometry();

            using (var ctx = sg.Open())
            {
                bool first = true;

                foreach (var v in contour.Vertices)
                {
                    // start in part-local space centered at (0,0)
                    var local = new Vec2(v.X - offsetX, v.Y - offsetY);

                    // convert to WPF point
                    var p = new Point(local.X, local.Y);

                    // Apply mirror
                    if (mirror != null)
                        p = mirror.Transform(p);

                    // Apply rotation
                    p = rotate.Transform(p);

                    // Move to world space (plate coords)
                    p = worldTrans.Transform(p);

                    // Convert to screen coords (origin bottom-left)
                    double sx = origin.X + p.X * scale;
                    double sy = origin.Y - p.Y * scale;

                    var sp = new Point(sx, sy);

                    if (first)
                    {
                        ctx.BeginFigure(sp, true, true);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(sp, true, false);
                    }
                }
            }

            sg.Freeze();
            return sg;
        }


        // ───────────────────────────────────────────────
        // CENTERED TEXT
        // ───────────────────────────────────────────────
        private void DrawCenteredText(DrawingContext dc, string text)
        {
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                16,
                Brushes.DarkGray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var point = new Point(
                (ActualWidth - ft.Width) / 2.0,
                (ActualHeight - ft.Height) / 2.0);

            dc.DrawText(ft, point);
        }
    }
}
