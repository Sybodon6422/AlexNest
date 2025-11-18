using AlexNest.Core.Geometry;

namespace AlexNest.Core.Model;

public class NestContour
{
    /// <summary>
    /// True = outer profile, false = inner hole.
    /// </summary>
    public bool IsOuter { get; set; }

    /// <summary>
    /// Closed polygon; last point is NOT duplicated (we close implicitly).
    /// Must be CCW for outer, CW for holes ideally, but we won't rely on it yet.
    /// </summary>
    public List<Vec2> Vertices { get; } = new();

    public Rect2D GetBounds() => Rect2D.FromPoints(Vertices);

    public double GetSignedArea()
    {
        double area = 0;
        int n = Vertices.Count;
        if (n < 3) return 0;

        for (int i = 0; i < n; i++)
        {
            var p0 = Vertices[i];
            var p1 = Vertices[(i + 1) % n];
            area += p0.X * p1.Y - p1.X * p0.Y;
        }

        return 0.5 * area;
    }

    public NestContour Transform(Vec2 translation, double rotationRadians)
    {
        var result = new NestContour { IsOuter = IsOuter };
        foreach (var v in Vertices)
        {
            var rv = Vec2.Rotate(v, rotationRadians) + translation;
            result.Vertices.Add(rv);
        }
        return result;
    }
}
