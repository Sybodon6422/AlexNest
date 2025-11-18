using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.Core.Algorithms;

public class GridNesterSettings
{
    /// <summary>
    /// Grid step size in same units as geometry (e.g. mm or inches).
    /// </summary>
    public double GridStep { get; set; } = 5.0;

    /// <summary>
    /// Extra distance between parts (clearance), not including kerf.
    /// </summary>
    public double Clearance { get; set; } = 1.0;

    /// <summary>
    /// Kerf width (cut width).
    /// </summary>
    public double Kerf { get; set; } = 1.5;

    /// <summary>
    /// Allowed rotations in degrees. If null, auto-generate from RotationStepDeg of parts.
    /// </summary>
    public List<double>? AllowedRotationsDeg { get; set; }
}

public class NestingResult
{
    public List<PartPlacement> Placements { get; } = new();
    public List<NestPart> UnplacedParts { get; } = new();
}

public class GridNester
{
    public NestingResult Nest(IEnumerable<NestPart> parts, NestPlate plate, GridNesterSettings settings)
    {
        // Ensure parts have bounds & area
        foreach (var part in parts)
            part.RecalculateProperties();

        // explode quantities into a flat list of instances
        var instances = new List<NestPart>();
        foreach (var p in parts)
        {
            for (int i = 0; i < p.Quantity; i++)
                instances.Add(p);
        }

        // Sort biggest area first to reduce trapping
        instances = instances
            .OrderByDescending(p => p.Area)
            .ToList();

        var placements = new List<PartPlacement>();
        var unplaced = new List<NestPart>();

        foreach (var part in instances)
        {
            if (!TryPlacePart(part, plate, settings, placements, out var placement))
            {
                unplaced.Add(part);
            }
            else
            {
                placements.Add(placement);
            }
        }

        return new NestingResult
        {
            Placements = placements,
            UnplacedParts = unplaced
        };
    }

    private bool TryPlacePart(
        NestPart part,
        NestPlate plate,
        GridNesterSettings settings,
        List<PartPlacement> existing,
        out PartPlacement placement)
    {
        placement = null!;

        var allowedRotations = settings.AllowedRotationsDeg;
        if (allowedRotations == null || allowedRotations.Count == 0)
        {
            allowedRotations = new List<double>();
            double step = part.RotationStepDeg;
            double n = Math.Max(1, Math.Round(360.0 / step));
            for (int i = 0; i < (int)n; i++)
                allowedRotations.Add(i * step);
        }

        double spacing = settings.Clearance + settings.Kerf;

        // We’ll work in local coordinates where part.Bounds.BottomLeft is (0,0)
        var localOffset = new Vec2(-part.Bounds.MinX, -part.Bounds.MinY);

        foreach (var rotDeg in allowedRotations)
        {
            double rotRad = rotDeg * Math.PI / 180.0;

            // Precompute rotated contours so we can reuse them at different translations
            var rotatedContours = part.Contours
                .Select(c => c.Transform(localOffset, rotRad))
                .ToList();

            var rotatedAllPoints = rotatedContours.SelectMany(c => c.Vertices).ToList();
            var rotatedBounds = Rect2D.FromPoints(rotatedAllPoints);

            double step = settings.GridStep;

            for (double y = 0; y + rotatedBounds.Height <= plate.Height + 1e-6; y += step)
            {
                for (double x = 0; x + rotatedBounds.Width <= plate.Width + 1e-6; x += step)
                {
                    var translation = new Vec2(x - rotatedBounds.MinX, y - rotatedBounds.MinY);

                    // simple AABB for the placed part
                    var placedBounds = rotatedBounds.Translate(translation);

                    // Quick check: must be inside plate rectangle with spacing from edges
                    if (placedBounds.MinX < spacing ||
                        placedBounds.MinY < spacing ||
                        placedBounds.MaxX > plate.Width - spacing ||
                        placedBounds.MaxY > plate.Height - spacing)
                    {
                        continue;
                    }

                    // Collision with existing placements (AABB first, then polygon)
                    bool collision = false;
                    foreach (var e in existing)
                    {
                        // fast AABB check with spacing
                        var expanded = new Rect2D(
                            e.Bounds.MinX - spacing,
                            e.Bounds.MinY - spacing,
                            e.Bounds.MaxX + spacing,
                            e.Bounds.MaxY + spacing);

                        if (!placedBounds.Intersects(expanded))
                            continue;

                        // slower polygon check
                        if (PolygonsOverlap(rotatedContours, translation, e.Part, e.Position, e.RotationDeg))
                        {
                            collision = true;
                            break;
                        }
                    }

                    if (!collision)
                    {
                        placement = new PartPlacement(part, translation, rotDeg, placedBounds);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool PolygonsOverlap(
        List<NestContour> contoursA, Vec2 translationA,
        NestPart partB, Vec2 translationB, double rotationBDeg)
    {
        double rotBRad = rotationBDeg * Math.PI / 180.0;
        var offsetB = new Vec2(-partB.Bounds.MinX, -partB.Bounds.MinY);

        // Build transformed polygons for B
        var contoursB = partB.Contours
            .Select(c => c.Transform(offsetB, rotBRad))
            .ToList();

        foreach (var cB in contoursB)
        {
            for (int i = 0; i < cB.Vertices.Count; i++)
                cB.Vertices[i] = cB.Vertices[i] + translationB;
        }

        // Build transformed polygons for A (we already have rotated, we just add translation)
        var transformedA = contoursA.Select(c =>
        {
            var nc = new NestContour { IsOuter = c.IsOuter };
            foreach (var v in c.Vertices)
                nc.Vertices.Add(v + translationA);
            return nc;
        }).ToList();

        foreach (var ca in transformedA)
        {
            foreach (var cb in contoursB)
            {
                if (PolygonUtils.PolygonsIntersect(ca.Vertices, cb.Vertices))
                    return true;
            }
        }

        return false;
    }
}
