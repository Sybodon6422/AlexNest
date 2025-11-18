using netDxf;
using netDxf.Entities;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.IO.Dxf;

public class DXFPlateImportOptions
{
    public string PlateOuterLayer { get; set; } = "PLATE";
    public string VoidLayer { get; set; } = "VOID";
}

public static class DXFPlateImporter
{
    public static NestPlate ImportPlate(string path, DxfPlateImportOptions? options = null)
    {
        options ??= new DxfPlateImportOptions();
        var doc = DxfDocument.Load(path);

        // Read outer plate boundary
        NestContour? outer = null;

        // Try LwPolylines on PlateOuterLayer
        foreach (var lw in doc.LwPolylines)
        {
            if (!lw.IsClosed) continue;
            if (!string.Equals(lw.Layer.Name, options.PlateOuterLayer, StringComparison.OrdinalIgnoreCase))
                continue;

            var contour = new NestContour { IsOuter = true };
            foreach (var v in lw.Vertexes)
                contour.Vertices.Add(new Vec2(v.Position.X, v.Position.Y));

            outer = contour;
            break;
        }

        // Fallback: first closed Polyline if needed
        if (outer == null)
        {
            foreach (var pl in doc.Polylines)
            {
                if (!pl.IsClosed) continue;

                var contour = new NestContour { IsOuter = true };
                foreach (var v in pl.Vertexes)
                    contour.Vertices.Add(new Vec2(v.Position.X, v.Position.Y));

                outer = contour;
                break;
            }
        }

        if (outer == null)
            throw new InvalidOperationException("No closed polyline found for plate outer boundary.");

        var bounds = outer.GetBounds();

        var plate = new NestPlate
        {
            Width = bounds.Width,
            Height = bounds.Height
        };

        // Later you can add:
        // plate.OuterBoundary = outer;
        // and import voids from options.VoidLayer

        return plate;
    }
}
