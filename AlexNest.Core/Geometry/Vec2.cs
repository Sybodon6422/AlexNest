namespace AlexNest.Core.Geometry;

public readonly struct Vec2
{
    public double X { get; }
    public double Y { get; }

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double Dot(Vec2 other) => X * other.X + Y * other.Y;

    public double Length() => Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalize()
    {
        var len = Length();
        return len > 0 ? this / len : this;
    }

    public static Vec2 Rotate(Vec2 v, double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new Vec2(
            v.X * c - v.Y * s,
            v.X * s + v.Y * c
        );
    }

    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
