using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.IO.DXF;

public class DxfPartImportOptions
{
    /// <summary>
    /// If set, only these layers are used as parts. If null/empty, all layers are used.
    /// </summary>
    public List<string>? PartLayers { get; set; }

    /// <summary>
    /// If true, each closed shape becomes its own part.
    /// If false, all shapes are merged into a single part.
    /// </summary>
    public bool EachClosedShapeIsPart { get; set; } = true;

    public string SinglePartName { get; set; } = "Part";
}

public static class DxfPartImporter
{
    public static List<NestPart> ImportParts(string path, DxfPartImportOptions? options = null)
    {
        options ??= new DxfPartImportOptions();

        // IxMilia: loads ALL DXF versions 1.0 → R2018, so no header hacks needed
        var file = DxfFile.Load(path);

        bool FilterLayer(DxfEntity e)
        {
            if (options.PartLayers == null || options.PartLayers.Count == 0)
                return true;
            return options.PartLayers.Contains(e.Layer, StringComparer.OrdinalIgnoreCase);
        }

        var contours = new List<NestContour>();

        // LWPOLYLINEs
        foreach (var lw in file.Entities.OfType<DxfLwPolyline>().Where(FilterLayer))
        {
            if (!lw.IsClosed) continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in lw.Vertices)
                contour.Vertices.Add(new Vec2(v.X, v.Y));

            contours.Add(contour);
        }

        // POLYLINEs
        foreach (var pl in file.Entities.OfType<DxfPolyline>().Where(FilterLayer))
        {
            if (!pl.IsClosed) continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in pl.Vertices)
                contour.Vertices.Add(new Vec2(v.Location.X, v.Location.Y));

            contours.Add(contour);
        }

        // CIRCLEs → approximate as polygon
        foreach (var c in file.Entities.OfType<DxfCircle>().Where(FilterLayer))
        {
            var contour = new NestContour { IsOuter = true };

            const int segments = 32; // tweak if you want smoother circles
            for (int i = 0; i < segments; i++)
            {
                double ang = 2.0 * Math.PI * i / segments;
                double x = c.Center.X + c.Radius * Math.Cos(ang);
                double y = c.Center.Y + c.Radius * Math.Sin(ang);
                contour.Vertices.Add(new Vec2(x, y));
            }

            contours.Add(contour);
        }

        // classify CCW (outer) vs CW (hole) by signed area
        foreach (var c in contours)
            c.IsOuter = c.GetSignedArea() > 0;

        if (options.EachClosedShapeIsPart)
        {
            var parts = new List<NestPart>();
            int i = 1;

            foreach (var contour in contours)
            {
                var part = new NestPart
                {
                    Name = $"Part_{i}",
                    Quantity = 1,
                    RotationStepDeg = 90
                };
                part.Contours.Add(contour);
                part.RecalculateProperties();
                parts.Add(part);
                i++;
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
