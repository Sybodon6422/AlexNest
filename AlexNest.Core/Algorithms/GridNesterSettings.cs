namespace AlexNest.Core.Algorithms
{
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
        public bool AllowMirror { get; set; } = false;
    }
}
