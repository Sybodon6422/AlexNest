using System;
using System.Collections.Generic;
using System.Linq;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.IO.DXF;

public class DxfPlateImportOptions
{
    /// <summary>
    /// Layer name for the outer plate boundary. If null/empty, any layer is allowed.
    /// </summary>
    public string? PlateOuterLayer { get; set; } = "PLATE";

    /// <summary>
    /// Layer for voids / cutouts (not used yet in the simple grid nester).
    /// </summary>
    public string VoidLayer { get; set; } = "VOID";
}

public static class DxfPlateImporter
{
    public static NestPlate ImportPlate(string path, DxfPlateImportOptions? options = null)
    {
        options ??= new DxfPlateImportOptions();

        var file = DxfFile.Load(path);

        NestContour? outer = TryGetOuterContourFromPolylines(file, options);

        if (outer != null)
        {
            var b = outer.GetBounds();
            return new NestPlate
            {
                Width = b.Width,
                Height = b.Height
            };
        }

        // Fallback: use extents of ALL geometry as plate rectangle
        var (minX, minY, maxX, maxY) = ComputeExtents(file);

        if (double.IsNaN(minX) || double.IsNaN(minY) ||
            double.IsNaN(maxX) || double.IsNaN(maxY))
        {
            throw new InvalidOperationException(
                "DXF has no geometry that can be used to determine plate extents.");
        }

        return new NestPlate
        {
            Width = maxX - minX,
            Height = maxY - minY
        };
    }

    private static NestContour? TryGetOuterContourFromPolylines(DxfFile file, DxfPlateImportOptions options)
    {
        bool LayerMatches(string entityLayer)
        {
            if (string.IsNullOrWhiteSpace(options.PlateOuterLayer))
                return true; // accept any layer
            return string.Equals(entityLayer, options.PlateOuterLayer, StringComparison.OrdinalIgnoreCase);
        }

        // Prefer LWPOLYLINE on the plate layer
        var lwOuter = file.Entities
            .OfType<DxfLwPolyline>()
            .FirstOrDefault(e => e.IsClosed && LayerMatches(e.Layer));

        if (lwOuter != null)
        {
            var contour = new NestContour { IsOuter = true };
            foreach (var v in lwOuter.Vertices)
                contour.Vertices.Add(new Vec2(v.X, v.Y));
            return contour;
        }

        // Fallback: POLYLINE (2D)
        var plOuter = file.Entities
            .OfType<DxfPolyline>()
            .FirstOrDefault(e => e.IsClosed && LayerMatches(e.Layer));

        if (plOuter != null)
        {
            var contour = new NestContour { IsOuter = true };
            foreach (var v in plOuter.Vertices)
                contour.Vertices.Add(new Vec2(v.Location.X, v.Location.Y));
            return contour;
        }

        // Final fallback: any closed LWPOLYLINE, any layer
        var anyLw = file.Entities.OfType<DxfLwPolyline>().FirstOrDefault(e => e.IsClosed);
        if (anyLw != null)
        {
            var contour = new NestContour { IsOuter = true };
            foreach (var v in anyLw.Vertices)
                contour.Vertices.Add(new Vec2(v.X, v.Y));
            return contour;
        }

        // Or any closed POLYLINE
        var anyPl = file.Entities.OfType<DxfPolyline>().FirstOrDefault(e => e.IsClosed);
        if (anyPl != null)
        {
            var contour = new NestContour { IsOuter = true };
            foreach (var v in anyPl.Vertices)
                contour.Vertices.Add(new Vec2(v.Location.X, v.Location.Y));
            return contour;
        }

        return null;
    }

    private static (double minX, double minY, double maxX, double maxY) ComputeExtents(DxfFile file)
    {
        bool hasAny = false;
        double minX = double.NaN, minY = double.NaN, maxX = double.NaN, maxY = double.NaN;

        void AddPoint(double x, double y)
        {
            if (!hasAny)
            {
                minX = maxX = x;
                minY = maxY = y;
                hasAny = true;
            }
            else
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        foreach (var e in file.Entities)
        {
            switch (e)
            {
                case DxfLine ln:
                    AddPoint(ln.P1.X, ln.P1.Y);
                    AddPoint(ln.P2.X, ln.P2.Y);
                    break;

                case DxfLwPolyline lw:
                    foreach (var v in lw.Vertices)
                        AddPoint(v.X, v.Y);
                    break;

                case DxfPolyline pl:
                    foreach (var v in pl.Vertices)
                        AddPoint(v.Location.X, v.Location.Y);
                    break;

                case DxfCircle c:
                    AddPoint(c.Center.X - c.Radius, c.Center.Y - c.Radius);
                    AddPoint(c.Center.X + c.Radius, c.Center.Y + c.Radius);
                    break;

/*                case DxfArc a:
                    // Rough extents: full circle box (simpler than exact arc-box)
                    AddPoint(a.Center.X - a.Radius, a.Center.Y - a.Radius);
                    AddPoint(a.Center.X + a.Radius, a.Center.Y + a.Radius);
                    break;*/
            }
        }

        return (minX, minY, maxX, maxY);
    }
}
