using System;
using System.Collections.Generic;
using System.Linq;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.IO.DXF;

public class DxfPartImportOptions
{
    /// <summary>
    /// If set, only entities on these layers are imported as part geometry.
    /// If null or empty, all layers are used.
    /// </summary>
    public List<string>? PartLayers { get; set; }

    /// <summary>
    /// If true, each closed shape (polyline/circle) becomes its own part.
    /// If false, everything is merged into a single part.
    /// NOTE: in the line-based reconstruction and fallback, we always return a single part.
    /// </summary>
    public bool EachClosedShapeIsPart { get; set; } = true;

    /// <summary>
    /// Name to use when building a single merged part.
    /// </summary>
    public string SinglePartName { get; set; } = "Part";
}

public static class DxfPartImporter
{
    public static List<NestPart> ImportParts(string path, DxfPartImportOptions? options = null)
    {
        options ??= new DxfPartImportOptions();

        var file = DxfFile.Load(path);

        bool FilterLayer(DxfEntity e)
        {
            if (options.PartLayers == null || options.PartLayers.Count == 0)
                return true;
            return options.PartLayers.Contains(e.Layer, StringComparer.OrdinalIgnoreCase);
        }

        var polyContours = new List<NestContour>();
        var circleContours = new List<NestContour>();

        // --- Closed LWPOLYLINEs ---
        foreach (var lw in file.Entities.OfType<DxfLwPolyline>().Where(FilterLayer))
        {
            if (!lw.IsClosed) continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in lw.Vertices)
                contour.Vertices.Add(new Vec2(v.X, v.Y));

            polyContours.Add(contour);
        }

        // --- Closed POLYLINEs ---
        foreach (var pl in file.Entities.OfType<DxfPolyline>().Where(FilterLayer))
        {
            if (!pl.IsClosed) continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in pl.Vertices)
                contour.Vertices.Add(new Vec2(v.Location.X, v.Location.Y));

            polyContours.Add(contour);
        }

        // --- CIRCLEs ---
        foreach (var c in file.Entities.OfType<DxfCircle>().Where(FilterLayer))
        {
            var contour = new NestContour { IsOuter = true };
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                double ang = 2.0 * Math.PI * i / segments;
                double x = c.Center.X + c.Radius * Math.Cos(ang);
                double y = c.Center.Y + c.Radius * Math.Sin(ang);
                contour.Vertices.Add(new Vec2(x, y));
            }
            circleContours.Add(contour);
        }

        // ---------- CASE A: we have proper closed polylines ----------
        if (polyContours.Count > 0)
        {
            // classify CCW (outer) vs CW (hole) by signed area
            foreach (var c in polyContours)
                c.IsOuter = c.GetSignedArea() > 0;

            foreach (var c in circleContours)
                c.IsOuter = c.GetSignedArea() > 0;

            var allContours = new List<NestContour>();
            allContours.AddRange(polyContours);
            allContours.AddRange(circleContours);

            if (options.EachClosedShapeIsPart)
            {
                var parts = new List<NestPart>();
                int i = 1;
                foreach (var contour in allContours)
                {
                    var p = new NestPart
                    {
                        Name = $"Part_{i}",
                        Quantity = 1,
                        RotationStepDeg = 90
                    };
                    p.Contours.Add(contour);
                    p.RecalculateProperties();
                    parts.Add(p);
                    i++;
                }
                return parts;
            }
            else
            {
                var p = new NestPart
                {
                    Name = options.SinglePartName,
                    Quantity = 1,
                    RotationStepDeg = 90
                };
                foreach (var c in allContours)
                    p.Contours.Add(c);
                p.RecalculateProperties();
                return new List<NestPart> { p };
            }
        }

        // ---------- CASE B: no closed polylines → try to build polygon from LINES ----------
        var lineEntities = file.Entities.OfType<DxfLine>().Where(FilterLayer).ToList();

        if (lineEntities.Count >= 3)
        {
            var reconstructed = TryBuildPolygonFromLines(lineEntities);

            if (reconstructed != null && reconstructed.Count >= 3)
            {
                var outerContour = new NestContour { IsOuter = true };
                outerContour.Vertices.AddRange(reconstructed);

                // Treat circles as holes in this part when using line-based contour
                foreach (var hole in circleContours)
                    hole.IsOuter = false;

                var part = new NestPart
                {
                    Name = options.SinglePartName,
                    Quantity = 1,
                    RotationStepDeg = 90
                };

                part.Contours.Add(outerContour);
                foreach (var hole in circleContours)
                    part.Contours.Add(hole);

                part.RecalculateProperties();
                return new List<NestPart> { part };
            }
        }

        // ---------- CASE C: fallback – extents rectangle + holes ----------
        var filtered = file.Entities.Where(FilterLayer).ToList();
        if (filtered.Count == 0)
            throw new InvalidOperationException("DXF contains no entities on the selected part layers.");

        var (minX, minY, maxX, maxY) = ComputeExtents(filtered);
        if (double.IsNaN(minX) || double.IsNaN(minY) ||
            double.IsNaN(maxX) || double.IsNaN(maxY))
        {
            throw new InvalidOperationException(
                "DXF has no geometry that can be used to determine part extents.");
        }

        var outerRect = new NestContour { IsOuter = true };
        outerRect.Vertices.Add(new Vec2(minX, minY));
        outerRect.Vertices.Add(new Vec2(maxX, minY));
        outerRect.Vertices.Add(new Vec2(maxX, maxY));
        outerRect.Vertices.Add(new Vec2(minX, maxY));

        foreach (var hole in circleContours)
            hole.IsOuter = false;

        var fallbackPart = new NestPart
        {
            Name = options.SinglePartName,
            Quantity = 1,
            RotationStepDeg = 90
        };
        fallbackPart.Contours.Add(outerRect);
        foreach (var hole in circleContours)
            fallbackPart.Contours.Add(hole);
        fallbackPart.RecalculateProperties();

        return new List<NestPart> { fallbackPart };
    }

    // Try to walk a single closed polygon from line segments
    private static List<Vec2>? TryBuildPolygonFromLines(List<DxfLine> lines)
    {
        if (lines.Count == 0)
            return null;

        const double EPS = 1e-6;

        static bool Near(Vec2 a, Vec2 b)
        {
            return Math.Abs(a.X - b.X) < EPS && Math.Abs(a.Y - b.Y) < EPS;
        }

        // simple walk-based reconstruction: follow connected edges until we loop
        var pts = new List<Vec2>();
        var visited = new bool[lines.Count];

        Vec2 GetP1(int i) => new(lines[i].P1.X, lines[i].P1.Y);
        Vec2 GetP2(int i) => new(lines[i].P2.X, lines[i].P2.Y);

        // start from first line
        var start = GetP1(0);
        var current = start;
        pts.Add(current);

        for (int step = 0; step < lines.Count + 1; step++)
        {
            int nextIndex = -1;
            bool nextFromP1 = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (visited[i]) continue;
                var p1 = GetP1(i);
                var p2 = GetP2(i);

                if (Near(p1, current))
                {
                    nextIndex = i;
                    nextFromP1 = true;
                    break;
                }
                if (Near(p2, current))
                {
                    nextIndex = i;
                    nextFromP1 = false;
                    break;
                }
            }

            if (nextIndex < 0)
                break; // no continuation

            visited[nextIndex] = true;

            var nextPoint = nextFromP1 ? GetP2(nextIndex) : GetP1(nextIndex);
            pts.Add(nextPoint);
            current = nextPoint;

            // closed?
            if (Near(current, start) && pts.Count >= 4) // triangle: 3 unique + repeat
            {
                // ensure last point equals first exactly
                if (!Near(pts[0], pts[^1]))
                    pts.Add(pts[0]);
                return pts;
            }
        }

        return null;
    }

    private static (double minX, double minY, double maxX, double maxY)
        ComputeExtents(IEnumerable<DxfEntity> entities)
    {
        bool hasAny = false;
        double minX = double.NaN, minY = double.NaN, maxX = double.NaN, maxY = double.NaN;

        void AddPoint(double x, double y)
        {
            if (!hasAny)
            {
                minX = maxX = x;
                minY = maxY = y;
                hasAny = true;
            }
            else
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        foreach (var e in entities)
        {
            if (e is DxfLine ln)
            {
                AddPoint(ln.P1.X, ln.P1.Y);
                AddPoint(ln.P2.X, ln.P2.Y);
            }
            else if (e is DxfLwPolyline lw)
            {
                foreach (var v in lw.Vertices)
                    AddPoint(v.X, v.Y);
            }
            else if (e is DxfPolyline pl)
            {
                foreach (var v in pl.Vertices)
                    AddPoint(v.Location.X, v.Location.Y);
            }
            else if (e is DxfCircle c)
            {
                AddPoint(c.Center.X - c.Radius, c.Center.Y - c.Radius);
                AddPoint(c.Center.X + c.Radius, c.Center.Y + c.Radius);
            }
            else if (e is DxfArc a)
            {
                AddPoint(a.Center.X - a.Radius, a.Center.Y - a.Radius);
                AddPoint(a.Center.X + a.Radius, a.Center.Y + a.Radius);
            }
        }

        return (minX, minY, maxX, maxY);
    }
}
