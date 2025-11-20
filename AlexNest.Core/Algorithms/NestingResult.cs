using System.Collections.Generic;
using AlexNest.Core.Model;

namespace AlexNest.Core.Algorithms;

public class NestingResult
{
    public List<PartPlacement> Placements { get; } = new();
    public List<NestPart> UnplacedParts { get; } = new();
}