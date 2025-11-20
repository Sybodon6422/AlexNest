using System;
using System.Collections.Generic;
using System.Linq;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.Core.Algorithms
{
    /// <summary>
    /// Simple strip/row-based nester:
    /// - Fills a row left-to-right until width is exceeded
    /// - Then moves to the next row
    /// - No rotation (RotationRad = 0)
    /// - Uses bounding boxes and NestPart.Quantity
    /// </summary>
    public class StripNester : INester
    {
        public NestingResult Nest(List<NestPart> parts, NestPlate plate, GridNesterSettings settings)
        {
            var result = new NestingResult();

            // Start in top-left corner with some clearance
            double x = settings.Clearance;
            double y = settings.Clearance;
            double rowHeight = 0.0;

            // Sort by height (tallest parts first)
            var ordered = parts
                .OrderByDescending(p => p.Bounds.Height)
                .ToList();

            foreach (var part in ordered)
            {
                int copies = Math.Max(1, part.Quantity);

                for (int copy = 0; copy < copies; copy++)
                {
                    double w = part.Bounds.Width + settings.Clearance;
                    double h = part.Bounds.Height + settings.Clearance;

                    // If placing this part would overflow the row, go to next row
                    if (x + w > plate.Width - settings.Clearance)
                    {
                        x = settings.Clearance;
                        y += rowHeight + settings.Clearance;
                        rowHeight = 0.0;
                    }

                    // If that would overflow plate height, we can't place this copy
                    if (y + h > plate.Height - settings.Clearance)
                    {
                        result.UnplacedParts.Add(part);
                        continue;
                    }

                    // Position is the center of the part's bounding box
                    double partCenterX = x + (w - settings.Clearance) / 2.0;
                    double partCenterY = y + (h - settings.Clearance) / 2.0;
                    var pos = new Vec2(partCenterX, partCenterY);

                    // Rotation is 0 for strip nesting
                    double rotRad = 0.0;

                    // Bounds rect in plate coordinates
                    double bbWidth = w - settings.Clearance;
                    double bbHeight = h - settings.Clearance;

                    var bounds = new Rect2D(
                        partCenterX - bbWidth / 2.0,
                        partCenterY - bbHeight / 2.0,
                        bbWidth,
                        bbHeight);

                    // Use your PartPlacement ctor: (NestPart, Vec2, double, Rect2D)
                    var placement = new PartPlacement(part, pos, rotRad, bounds);

                    // Optional mirroring flag if your PartPlacement has it
                    if (settings.AllowMirror)
                    {
                        placement.Mirrored = (copy % 2 == 1);
                    }

                    result.Placements.Add(placement);

                    // Advance strip position
                    x += w;
                    if (h > rowHeight)
                        rowHeight = h;
                }
            }

            return result;
        }
    }
}
