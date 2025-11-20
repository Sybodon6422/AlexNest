namespace AlexNest.Core.Geometry;

public static class ContourBuilder
{
    private const double EPS = 1e-6;

    public static List<Vec2> BuildContourFromSegments(List<Segment> segments, int arcResolution = 24)
    {
        if (segments.Count == 0)
            return new List<Vec2>();

        // Build adjacency by endpoints
        Dictionary<(double, double), List<Segment>> graph = new();

        Vec2 Key(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

        void Add(KeyValuePair<(double, double), Segment> kv)
        {
            if (!graph.ContainsKey(kv.Key))
                graph[kv.Key] = new List<Segment>();
            graph[kv.Key].Add(kv.Value);
        }

        foreach (var s in segments)
        {
            var k1 = Key(s.Start);
            var k2 = Key(s.End);

            Add(new KeyValuePair<(double, double), Segment>((k1.X, k1.Y), s));
            Add(new KeyValuePair<(double, double), Segment>((k2.X, k2.Y), s));
        }

        // Start at any segment start
        var startSeg = segments[0];
        Vec2 current = startSeg.Start;

        List<Vec2> path = new() { current };
        Segment? prev = null;

        while (true)
        {
            var key = (Math.Round(current.X, 6), Math.Round(current.Y, 6));
            if (!graph.ContainsKey(key))
                break;

            var next = graph[key].FirstOrDefault(s => s != prev);
            if (next == null)
                break;

            // Determine direction
            bool forward = (next.Start.X == current.X && next.Start.Y == current.Y);

            if (next is LineSegment ls)
            {
                var p = forward ? ls.End : ls.Start;
                path.Add(p);
                current = p;
            }
            else if (next is ArcSegment arc)
            {
                var pts = arc.BuildPoints(current, forward, arcResolution);
                foreach (var p in pts.Skip(1)) // first is current
                    path.Add(p);
                current = pts.Last();
            }

            prev = next;

            // If we returned to the first point AND built > 2 segments → closed
            if ((Math.Abs(current.X - path[0].X) < EPS &&
                 Math.Abs(current.Y - path[0].Y) < EPS) &&
                 path.Count > 3)
            {
                break;
            }
        }

        return path;
    }
}

public abstract class Segment
{
    public Vec2 Start { get; }
    public Vec2 End { get; }

    protected Segment(Vec2 start, Vec2 end)
    {
        Start = start;
        End = end;
    }
}

public class LineSegment : Segment
{
    public LineSegment(Vec2 s, Vec2 e) : base(s, e) { }
}

public class ArcSegment : Segment
{
    public Vec2 Center { get; }
    public double Radius { get; }
    public double StartAngleRad { get; }
    public double EndAngleRad { get; }
    public bool CCW { get; }

    public ArcSegment(Vec2 start, Vec2 end, Vec2 center, double r,
        double startAng, double endAng, bool ccw)
        : base(start, end)
    {
        Center = center;
        Radius = r;
        StartAngleRad = startAng;
        EndAngleRad = endAng;
        CCW = ccw;
    }

    public List<Vec2> BuildPoints(Vec2 current, bool forward, int arcResolution)
    {
        List<Vec2> pts = new();

        double a0 = StartAngleRad;
        double a1 = EndAngleRad;

        if (!forward) (a0, a1) = (a1, a0);

        double sweep = CCW == forward
            ? NormalizeAngle(a1 - a0)
            : NormalizeAngle(a0 - a1);

        int steps = Math.Max(4, arcResolution);
        double step = sweep / steps;

        for (int i = 0; i <= steps; i++)
        {
            double ang = a0 + step * i * (CCW == forward ? 1 : -1);
            pts.Add(new Vec2(
                Center.X + Radius * Math.Cos(ang),
                Center.Y + Radius * Math.Sin(ang)
            ));
        }

        return pts;
    }

    private static double NormalizeAngle(double a)
    {
        while (a < 0) a += Math.PI * 2;
        while (a >= Math.PI * 2) a -= Math.PI * 2;
        return a;
    }
}
