using System;
using System.Collections.Generic;

namespace MotionLab.Models
{
    /// <summary>
    /// Represents the final computed results and statistics of a completed test run.
    /// Suitable for JSON serialization.
    /// </summary>
    public class TestResults
    {
        public Guid TestId { get; set; } = Guid.NewGuid();
        public string Surface { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string TestMode { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        
        public int SampleCount { get; set; }
        public double EstimatedSamplingRateHz { get; set; }
        public double CalibrationFactor { get; set; }
        
        public double TotalDisplacement { get; set; }
        public double MeanVelocity { get; set; }
        public double PeakVelocity { get; set; }
        public double VelocityStandardDeviation { get; set; }
        public double CoefficientOfVariation { get; set; }
        public int StickSlipEventCount { get; set; }

        // Optional: raw samples can be included for export or discarded to save memory after writing.
        public List<MotionMeasurement> Measurements { get; set; } = new List<MotionMeasurement>();
    }
}
