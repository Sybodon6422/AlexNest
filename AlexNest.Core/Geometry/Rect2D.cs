namespace AlexNest.Core.Geometry;

public readonly struct Rect2D
{
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    public Rect2D(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public static Rect2D FromPoints(IEnumerable<Vec2> pts)
    {
        var e = pts.GetEnumerator();
        if (!e.MoveNext())
            throw new ArgumentException("Cannot create Rect2D from empty point set.");

        double minX = e.Current.X, maxX = e.Current.X;
        double minY = e.Current.Y, maxY = e.Current.Y;

        while (e.MoveNext())
        {
            var p = e.Current;
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new Rect2D(minX, minY, maxX, maxY);
    }

    public bool Intersects(Rect2D other)
    {
        return !(other.MinX >= MaxX ||
                 other.MaxX <= MinX ||
                 other.MinY >= MaxY ||
                 other.MaxY <= MinY);
    }

    public bool Contains(Vec2 p)
    {
        return p.X >= MinX && p.X <= MaxX &&
               p.Y >= MinY && p.Y <= MaxY;
    }

    public Rect2D Translate(Vec2 offset)
        => new(MinX + offset.X, MinY + offset.Y, MaxX + offset.X, MaxY + offset.Y);
}
