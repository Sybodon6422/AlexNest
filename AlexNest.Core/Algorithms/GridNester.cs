using System;
using System.Collections.Generic;
using System.Linq;
using AlexNest.Core.Geometry;
using AlexNest.Core.Model;

namespace AlexNest.Core.Algorithms
{
    public class GridNester : INester
    {
        public NestingResult Nest(List<NestPart> parts, NestPlate plate, GridNesterSettings settings)
        {
            var placements = new List<PartPlacement>();
            var unplaced = new List<NestPart>();

            // Place larger parts first (by bounding-box area)
            var orderedParts = parts
                .OrderByDescending(p => p.Bounds.Width * p.Bounds.Height)
                .ToList();

            // Grid dimensions in cells
            int gridWidth = (int)(plate.Width / settings.GridStep) + 2;
            int gridHeight = (int)(plate.Height / settings.GridStep) + 2;

            // Occupancy map
            bool[,] occupied = new bool[gridWidth, gridHeight];

            foreach (var part in orderedParts)
            {
                int copies = Math.Max(1, part.Quantity);

                for (int copy = 0; copy < copies; copy++)
                {
                    bool placedThisCopy = false;

                    foreach (var rotDeg in GetAllowedRotations(part))
                    {
                        double rotRad = rotDeg * Math.PI / 180.0;

                        // Rough rotated bounding box
                        double w = part.Bounds.Width;
                        double h = part.Bounds.Height;

                        double rotatedW =
                            Math.Abs(w * Math.Cos(rotRad)) +
                            Math.Abs(h * Math.Sin(rotRad));
                        double rotatedH =
                            Math.Abs(w * Math.Sin(rotRad)) +
                            Math.Abs(h * Math.Cos(rotRad));

                        // Cell count needed
                        int needCellsX = Math.Max(1, (int)Math.Ceiling(rotatedW / settings.GridStep));
                        int needCellsY = Math.Max(1, (int)Math.Ceiling(rotatedH / settings.GridStep));

                        for (int gy = 0; gy <= gridHeight - needCellsY && !placedThisCopy; gy++)
                        {
                            for (int gx = 0; gx <= gridWidth - needCellsX && !placedThisCopy; gx++)
                            {
                                var pos = new Vec2(
                                    gx * settings.GridStep + rotatedW / 2.0 + settings.Clearance,
                                    gy * settings.GridStep + rotatedH / 2.0 + settings.Clearance);

                                if (TryPlacePart(
                                        part,
                                        plate,
                                        pos,
                                        rotRad,
                                        settings,
                                        occupied,
                                        rotatedW,
                                        rotatedH,
                                        gx,
                                        gy,
                                        needCellsX,
                                        needCellsY,
                                        out var placement))
                                {
                                    // If your PartPlacement has Mirrored { get; set; }:
                                    if (settings.AllowMirror)
                                    {
                                        // simple heuristic: alternate mirrored copies
                                        placement.Mirrored = (copy % 2 == 1);
                                    }

                                    placements.Add(placement);
                                    MarkOccupied(occupied, gx, gy, needCellsX, needCellsY);
                                    placedThisCopy = true;
                                }
                            }
                        }
                    }

                    if (!placedThisCopy)
                        unplaced.Add(part);
                }
            }

            var result = new NestingResult();
            result.Placements.AddRange(placements);
            result.UnplacedParts.AddRange(unplaced);
            return result;
        }

        // Allowed rotations based on part.RotationStepDeg
        private IEnumerable<int> GetAllowedRotations(NestPart part)
        {
            if (part.RotationStepDeg <= 0)
            {
                yield return 0;
                yield break;
            }

            for (int r = 0; r < 360; r += (int)part.RotationStepDeg)
                yield return r;
        }

        private bool TryPlacePart(
            NestPart part,
            NestPlate plate,
            Vec2 pos,
            double rotRad,
            GridNesterSettings settings,
            bool[,] occupied,
            double rotatedW,
            double rotatedH,
            int gx,
            int gy,
            int needCellsX,
            int needCellsY,
            out PartPlacement placement)
        {
            placement = null!;

            // Plate boundary check (with clearance)
            double halfW = rotatedW / 2.0 + settings.Clearance;
            double halfH = rotatedH / 2.0 + settings.Clearance;

            if (pos.X - halfW < 0 ||
                pos.Y - halfH < 0 ||
                pos.X + halfW > plate.Width ||
                pos.Y + halfH > plate.Height)
            {
                return false;
            }

            // Check grid occupancy
            for (int yy = gy; yy < gy + needCellsY; yy++)
            {
                for (int xx = gx; xx < gx + needCellsX; xx++)
                {
                    if (occupied[xx, yy])
                        return false;
                }
            }

            // Create placement using YOUR constructor
            // Rect2D = bounding box in plate coordinates
            var bounds = new Rect2D(
                pos.X - rotatedW / 2.0,
                pos.Y - rotatedH / 2.0,
                rotatedW,
                rotatedH);

            placement = new PartPlacement(part, pos, rotRad, bounds);

            return true;
        }

        private void MarkOccupied(bool[,] occupied, int gx, int gy, int w, int h)
        {
            for (int yy = gy; yy < gy + h; yy++)
                for (int xx = gx; xx < gx + w; xx++)
                    occupied[xx, yy] = true;
        }
    }
}
