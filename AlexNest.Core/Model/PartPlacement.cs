using AlexNest.Core.Geometry;

namespace AlexNest.Core.Model;

public class PartPlacement
{
    public NestPart Part { get; }
    public Vec2 Position { get; }      // bottom-left of the part’s original bounds
    public double RotationDeg { get; }
    public Rect2D Bounds { get; }      // axis-aligned bounds after rotation+translation
    public bool Mirrored { get; set; } = false;
    public PartPlacement(NestPart part, Vec2 position, double rotationDeg, Rect2D bounds)
    {
        Part = part;
        Position = position;
        RotationDeg = rotationDeg;
        Bounds = bounds;
    }
}
