using System;

namespace MotionLab.Models
{
    /// <summary>
    /// Represents a raw data sample acquired from the hardware sensor.
    /// </summary>
    public readonly struct RawMotionDataPoint
    {
        /// <summary>
        /// High-resolution elapsed time from the start of the acquisition (e.g., from a Stopwatch).
        /// </summary>
        public TimeSpan Timestamp { get; }

        /// <summary>
        /// Raw X delta since the previous measurement in sensor-specific units (e.g., mickeys).
        /// </summary>
        public int DeltaX { get; }

        /// <summary>
        /// Raw Y delta since the previous measurement in sensor-specific units (e.g., mickeys).
        /// </summary>
        public int DeltaY { get; }

        public RawMotionDataPoint(TimeSpan timestamp, int deltaX, int deltaY)
        {
            Timestamp = timestamp;
            DeltaX = deltaX;
            DeltaY = deltaY;
        }
    }
}
