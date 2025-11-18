namespace AlexNest.Core.Model;

public class NestPlate
{
    /// <summary>
    /// Overall rectangular plate size used for basic nesting.
    /// Units (mm/inch) must match the part geometry.
    /// </summary>
    public double Width { get; set; }
    public double Height { get; set; }

    // Later we can add:
    // public NestContour? OuterBoundary { get; set; }
    // public List<NestContour> Voids { get; } = new();
}
