using AlexNest.Core.Geometry;

namespace AlexNest.Core.Algorithms;

public static class PolygonUtils
{
    public static bool PolygonsIntersect(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        // 1. Quick reject via bounds
        var rectA = Rect2D.FromPoints(a);
        var rectB = Rect2D.FromPoints(b);
        if (!rectA.Intersects(rectB))
            return false;

        // 2. Edge-edge intersection
        int nA = a.Count;
        int nB = b.Count;

        for (int i = 0; i < nA; i++)
        {
            Vec2 a0 = a[i];
            Vec2 a1 = a[(i + 1) % nA];

            for (int j = 0; j < nB; j++)
            {
                Vec2 b0 = b[j];
                Vec2 b1 = b[(j + 1) % nB];

                if (SegmentsIntersect(a0, a1, b0, b1))
                    return true;
            }
        }

        // 3. One polygon completely inside another
        if (PointInPolygon(a[0], b)) return true;
        if (PointInPolygon(b[0], a)) return true;

        return false;
    }

    public static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];

            bool intersect = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                             (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X);
            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    private static bool SegmentsIntersect(Vec2 p, Vec2 p2, Vec2 q, Vec2 q2)
    {
        // Using orientation / cross-product
        double o1 = Orientation(p, p2, q);
        double o2 = Orientation(p, p2, q2);
        double o3 = Orientation(q, q2, p);
        double o4 = Orientation(q, q2, p2);

        if (o1 * o2 < 0 && o3 * o4 < 0)
            return true;

        // Collinear cases (we can simplify and be slightly conservative)
        if (Math.Abs(o1) < 1e-9 && OnSegment(p, q, p2)) return true;
        if (Math.Abs(o2) < 1e-9 && OnSegment(p, q2, p2)) return true;
        if (Math.Abs(o3) < 1e-9 && OnSegment(q, p, q2)) return true;
        if (Math.Abs(o4) < 1e-9 && OnSegment(q, p2, q2)) return true;

        return false;
    }

    private static double Orientation(Vec2 a, Vec2 b, Vec2 c)
    {
        // cross((b - a), (c - a))
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private static bool OnSegment(Vec2 a, Vec2 b, Vec2 c)
    {
        return b.X <= Math.Max(a.X, c.X) + 1e-9 && b.X + 1e-9 >= Math.Min(a.X, c.X) &&
               b.Y <= Math.Max(a.Y, c.Y) + 1e-9 && b.Y + 1e-9 >= Math.Min(a.Y, c.Y);
    }
}
