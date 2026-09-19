namespace MotionLab.Models
{
    /// <summary>
    /// Configuration for a test run.
    /// </summary>
    public class TestConfig
    {
        /// <summary>
        /// The target sampling interval in milliseconds (e.g., 10 for ~100Hz).
        /// Note: Actual sampling rate is subject to Windows scheduling and polling jitter.
        /// </summary>
        public int SamplingIntervalMs { get; set; } = 10;
        
        /// <summary>
        /// Conversion factor from raw sensor units (e.g., mickeys) to physical units (e.g., mm).
        /// PhysicalDistance = RawUnits * CalibrationFactor.
        /// </summary>
        public double CalibrationFactor { get; set; } = 1.0;

        /// <summary>
        /// The surface being tested (e.g., "Wooden desk", "Mouse mat").
        /// </summary>
        public string Surface { get; set; } = "Default";

        /// <summary>
        /// The test mode (e.g., "Motion Test", "Repeatability Test").
        /// </summary>
        public string TestMode { get; set; } = "Motion Test";
    }
}
