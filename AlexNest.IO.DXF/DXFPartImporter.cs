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
        var arcEntities = file.Entities.OfType<DxfArc>().Where(FilterLayer).ToList();

        if (lineEntities.Count + arcEntities.Count >= 3)
        {
            var (outerLoop, holeLoops) = BuildLoopsFromLinesAndArcs(lineEntities, arcEntities, arcSegments: 32);

            if (outerLoop != null && outerLoop.Count >= 3)
            {
                var outerContour = new NestContour { IsOuter = true };
                outerContour.Vertices.AddRange(outerLoop);

                var part = new NestPart
                {
                    Name = options.SinglePartName,
                    Quantity = 1,
                    RotationStepDeg = 90
                };

                part.Contours.Add(outerContour);

                // holes from arc/line loops
                foreach (var hl in holeLoops)
                {
                    var hole = new NestContour { IsOuter = false };
                    hole.Vertices.AddRange(hl);
                    part.Contours.Add(hole);
                }

                // circles are also holes
                foreach (var hole in circleContours)
                {
                    hole.IsOuter = false;
                    part.Contours.Add(hole);
                }

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

    private static (List<Vec2>? outer, List<List<Vec2>> holes)
     BuildLoopsFromLinesAndArcs(
         List<DxfLine> lines,
         List<DxfArc> arcs,
         int arcSegments = 24)
    {
        const double EPS = 1e-5;

        static bool Near(Vec2 a, Vec2 b) =>
            Math.Abs(a.X - b.X) < EPS && Math.Abs(a.Y - b.Y) < EPS;

        // Build chains from lines
        var chains = new List<List<Vec2>>();
        foreach (var ln in lines)
        {
            chains.Add(new List<Vec2>
        {
            new Vec2(ln.P1.X, ln.P1.Y),
            new Vec2(ln.P2.X, ln.P2.Y)
        });
        }

        // Build chains from arcs (sampled)
        foreach (var a in arcs)
            chains.Add(SampleArcCCW(a, arcSegments));

        var loops = new List<List<Vec2>>();

        int safetyLoops = 0;
        int maxLoops = 200;   // hard cap
        int maxJoins = 5000;  // hard cap inside one loop

        while (chains.Count > 0 && safetyLoops++ < maxLoops)
        {
            // start a new loop with the first remaining chain
            var loop = new List<Vec2>(chains[0]);
            chains.RemoveAt(0);

            int safetyJoin = 0;
            while (chains.Count > 0 && safetyJoin++ < maxJoins)
            {
                bool joined = false;
                var head = loop[0];
                var tail = loop[^1];

                for (int i = 0; i < chains.Count; i++)
                {
                    var c = chains[i];
                    var cHead = c[0];
                    var cTail = c[^1];

                    if (Near(tail, cHead))
                    {
                        loop.AddRange(c.Skip(1));
                        chains.RemoveAt(i);
                        joined = true;
                        break;
                    }
                    if (Near(tail, cTail))
                    {
                        c.Reverse();
                        loop.AddRange(c.Skip(1));
                        chains.RemoveAt(i);
                        joined = true;
                        break;
                    }
                    if (Near(head, cTail))
                    {
                        loop.InsertRange(0, c.Take(c.Count - 1));
                        chains.RemoveAt(i);
                        joined = true;
                        break;
                    }
                    if (Near(head, cHead))
                    {
                        c.Reverse();
                        loop.InsertRange(0, c.Take(c.Count - 1));
                        chains.RemoveAt(i);
                        joined = true;
                        break;
                    }
                }

                if (!joined)
                    break;

                // closed?
                if (loop.Count >= 4 && Near(loop[0], loop[^1]))
                    break;
            }

            if (loop.Count >= 4 && Near(loop[0], loop[^1]))
            {
                loop = CleanupPath(loop);
                loops.Add(loop);
            }
        }

        if (loops.Count == 0)
            return (null, new List<List<Vec2>>());

        // Pick largest area magnitude loop as outer
        var ordered = loops
            .Select(l => (loop: l, area: Math.Abs(SignedArea(l))))
            .OrderByDescending(x => x.area)
            .ToList();

        var outer = ordered[0].loop;
        var holes = ordered.Skip(1).Select(x => x.loop).ToList();

        return (outer, holes);
    }


    private static List<Vec2> SampleArcCCW(DxfArc arc, int baseSegments)
    {
        baseSegments = Math.Max(6, baseSegments);

        var c = arc.Center;
        double r = arc.Radius;

        double start = arc.StartAngle * Math.PI / 180.0;
        double end = arc.EndAngle * Math.PI / 180.0;

        // DXF arcs are CCW from start to end
        double sweep = end - start;
        while (sweep <= 0) sweep += Math.PI * 2;

        // adaptive segments by arc length, capped
        double arcLen = sweep * r;
        int segments = Math.Max(baseSegments, (int)(arcLen / 0.05));
        segments = Math.Min(segments, 256);

        var pts = new List<Vec2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            double t = i / (double)segments;
            double ang = start + sweep * t;

            pts.Add(new Vec2(
                c.X + r * Math.Cos(ang),
                c.Y + r * Math.Sin(ang)
            ));
        }

        return pts;
    }

    private static double SignedArea(List<Vec2> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p = pts[i];
            var q = pts[i + 1];
            a += (p.X * q.Y - q.X * p.Y);
        }
        return 0.5 * a;
    }

    private static List<Vec2> CleanupPath(List<Vec2> pts, double eps = 1e-5)
    {
        if (pts.Count < 3) return pts;

        // remove near duplicates
        var dedup = new List<Vec2> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            var last = dedup[^1];
            if (Math.Abs(p.X - last.X) > eps || Math.Abs(p.Y - last.Y) > eps)
                dedup.Add(p);
        }

        if (dedup.Count < 3) return dedup;

        // remove collinear midpoints
        var clean = new List<Vec2> { dedup[0] };
        for (int i = 1; i < dedup.Count - 1; i++)
        {
            var a = clean[^1];
            var b = dedup[i];
            var c = dedup[i + 1];

            var ab = b - a;
            var bc = c - b;

            double cross = ab.X * bc.Y - ab.Y * bc.X;
            if (Math.Abs(cross) > eps)
                clean.Add(b);
        }
        clean.Add(dedup[^1]);
        return clean;
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

    private static List<Vec2> SampleArc(DxfArc arc, int segments)
    {
        segments = Math.Max(6, segments);

        var c = arc.Center;
        double r = arc.Radius;

        // IxMilia uses degrees
        double start = arc.StartAngle * Math.PI / 180.0;
        double end = arc.EndAngle * Math.PI / 180.0;

        // Normalize sweep CCW
        double sweep = end - start;
        while (sweep <= 0) sweep += Math.PI * 2;

        var pts = new List<Vec2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            double t = i / (double)segments;
            double ang = start + sweep * t;

            pts.Add(new Vec2(
                c.X + r * Math.Cos(ang),
                c.Y + r * Math.Sin(ang)
            ));
        }

        return pts;
    }

}
