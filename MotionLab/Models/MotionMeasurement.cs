using System;

namespace MotionLab.Models
{
    /// <summary>
    /// Represents processed kinematic measurements derived from raw data.
    /// Uses physical distance units if calibration is applied.
    /// </summary>
    public readonly struct MotionMeasurement
    {
        public TimeSpan Timestamp { get; }
        public double XDisplacement { get; }
        public double YDisplacement { get; }
        public double PathDisplacement { get; }
        public double InstantaneousVelocity { get; }
        
        public MotionMeasurement(TimeSpan timestamp, double xDisplacement, double yDisplacement, double pathDisplacement, double instantaneousVelocity)
        {
            Timestamp = timestamp;
            XDisplacement = xDisplacement;
            YDisplacement = yDisplacement;
            PathDisplacement = pathDisplacement;
            InstantaneousVelocity = instantaneousVelocity;
        }
    }
}
