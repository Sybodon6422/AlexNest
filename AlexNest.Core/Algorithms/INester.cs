using AlexNest.Core.Model;

namespace AlexNest.Core.Algorithms;

public enum NestAlgorithm
{
    Grid,
    Strip
}

public interface INester
{
    NestingResult Nest(List<NestPart> parts, NestPlate plate, GridNesterSettings settings);
}
