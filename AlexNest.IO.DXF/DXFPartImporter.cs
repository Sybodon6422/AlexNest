using netDxf;
using netDxf.Entities;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.IO.Dxf;

public class DXFPartImportOptions
{
    /// <summary>
    /// If set, only entities on these layers are imported as part geometry.
    /// If null or empty, all closed polylines/circles/arcs are considered.
    /// </summary>
    public List<string>? PartLayers { get; set; }

    /// <summary>
    /// If true, each closed polyline is treated as its own part.
    /// If false, all geometry in the file forms a single part.
    /// </summary>
    public bool EachClosedShapeIsPart { get; set; } = true;

    /// <summary>
    /// Name to give the single merged part (if EachClosedShapeIsPart=false).
    /// </summary>
    public string SinglePartName { get; set; } = "Part";
}

public static class DxfPartImporter
{
    public static List<NestPart> ImportParts(string path, DxfPartImportOptions? options = null)
    {
        options ??= new DxfPartImportOptions();
        var doc = DxfDocument.Load(path);

        bool FilterLayer(EntityObject e)
        {
            if (options.PartLayers == null || options.PartLayers.Count == 0)
                return true;
            return options.PartLayers.Contains(e.Layer.Name, StringComparer.OrdinalIgnoreCase);
        }

        // Collect contours
        var contours = new List<NestContour>();

        // LwPolylines (most common)
        foreach (var lw in doc.LwPolylines.Where(FilterLayer))
        {
            if (!lw.IsClosed) continue;

            var contour = new NestContour { IsOuter = true }; // we’ll fix IsOuter later by area
            foreach (var v in lw.Vertexes)
            {
                contour.Vertices.Add(new Vec2(v.Position.X, v.Position.Y));
            }
            contours.Add(contour);
        }

        // Regular Polylines
        foreach (var pl in doc.Polylines.Where(FilterLayer))
        {
            if (!pl.IsClosed) continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in pl.Vertexes)
            {
                contour.Vertices.Add(new Vec2(v.Position.X, v.Position.Y));
            }
            contours.Add(contour);
        }

        // Circles (approximate to polygon)
        foreach (var c in doc.Circles.Where(FilterLayer))
        {
            var contour = new NestContour { IsOuter = true };
            int segments = 32; // you can tweak this
            for (int i = 0; i < segments; i++)
            {
                double ang = 2 * Math.PI * i / segments;
                double x = c.Center.X + c.Radius * Math.Cos(ang);
                double y = c.Center.Y + c.Radius * Math.Sin(ang);
                contour.Vertices.Add(new Vec2(x, y));
            }
            contours.Add(contour);
        }

        // TODO: arcs if you want; for now we ignore them or you can approximate

        // Classify outer vs inner by area sign (CCW vs CW)
        foreach (var c in contours)
        {
            var area = c.GetSignedArea();
            // This convention: CCW => positive => outer
            c.IsOuter = area > 0;
        }

        if (options.EachClosedShapeIsPart)
        {
            var parts = new List<NestPart>();
            int index = 1;
            foreach (var c in contours)
            {
                var p = new NestPart
                {
                    Name = $"Part_{index}",
                    Quantity = 1,
                    RotationStepDeg = 90
                };
                p.Contours.Add(c);
                p.RecalculateProperties();
                parts.Add(p);
                index++;
            }
            return parts;
        }
        else
        {
            var part = new NestPart
            {
                Name = options.SinglePartName,
                Quantity = 1,
                RotationStepDeg = 90
            };

            foreach (var c in contours)
                part.Contours.Add(c);

            part.RecalculateProperties();
            return new List<NestPart> { part };
        }
    }
}
