using AlexNest.Core.Geometry;

namespace AlexNest.Core.Model;

public class NestPart
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// First outer contour is considered the main outline. Others may be islands.
    /// </summary>
    public List<NestContour> Contours { get; } = new();

    /// <summary>
    /// How many of this part we need to nest.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Rotation granularity in degrees (e.g., 90 = only 0,90,180,270).
    /// </summary>
    public double RotationStepDeg { get; set; } = 90.0;

    public Rect2D Bounds { get; private set; }
    public double Area { get; private set; }

    public void RecalculateProperties()
    {
        if (Contours.Count == 0)
        {
            Bounds = new Rect2D(0, 0, 0, 0);
            Area = 0;
            return;
        }

        var allPoints = Contours.SelectMany(c => c.Vertices).ToList();
        Bounds = Rect2D.FromPoints(allPoints);

        // Approx: sum outer areas - inner areas
        double area = 0;
        foreach (var c in Contours)
        {
            var a = c.GetSignedArea();
            if (c.IsOuter) area += Math.Abs(a);
            else area -= Math.Abs(a);
        }

        Area = Math.Abs(area);
    }
}
