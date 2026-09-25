using System;
using System.Linq;
using MotionLab.Models;

namespace MotionLab.Services.Statistics
{
    public interface IStatisticsService
    {
        void CalculateFinalStatistics(TestResults results);
    }

    public class StatisticsService : IStatisticsService
    {
        public void CalculateFinalStatistics(TestResults results)
        {
            if (results.Measurements == null || results.Measurements.Count == 0)
            {
                return;
            }

            var measurements = results.Measurements;
            
            results.SampleCount = measurements.Count;
            
            if (results.Duration.TotalSeconds > 0 && results.SampleCount > 1)
            {
                // Actual average sampling rate
                results.EstimatedSamplingRateHz = results.SampleCount / results.Duration.TotalSeconds;

                // Calculate Polling Jitter
                var deltas = new System.Collections.Generic.List<double>();
                for (int i = 1; i < measurements.Count; i++)
                {
                    deltas.Add((measurements[i].Timestamp - measurements[i - 1].Timestamp).TotalMilliseconds);
                }
                
                if (deltas.Any())
                {
                    double meanDelta = deltas.Average();
                    double sumOfSquares = deltas.Select(val => (val - meanDelta) * (val - meanDelta)).Sum();
                    results.PollingJitterMs = Math.Sqrt(sumOfSquares / deltas.Count);
                    if (meanDelta > 0)
                    {
                        results.PollingJitterPercentage = (results.PollingJitterMs / meanDelta) * 100.0;
                    }
                }
            }

            results.TotalDisplacement = measurements.Last().PathDisplacement;

            var velocities = measurements.Select(m => m.InstantaneousVelocity).ToList();
            
            if (velocities.Any())
            {
                results.MeanVelocity = velocities.Average();
                results.PeakVelocity = velocities.Max();
                
                // Calculate Standard Deviation
                double mean = results.MeanVelocity;
                double sumOfSquaresOfDifferences = velocities.Select(val => (val - mean) * (val - mean)).Sum();
                results.VelocityStandardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / velocities.Count);
                
                // Calculate Coefficient of Variation (CV) as a percentage
                if (results.MeanVelocity > 0)
                {
                    results.CoefficientOfVariation = (results.VelocityStandardDeviation / results.MeanVelocity) * 100.0;
                }

                // Calculate Acceleration
                var accelerations = new System.Collections.Generic.List<double>();
                for (int i = 1; i < measurements.Count; i++)
                {
                    double dt = (measurements[i].Timestamp - measurements[i - 1].Timestamp).TotalSeconds;
                    if (dt > 0)
                    {
                        double dv = measurements[i].InstantaneousVelocity - measurements[i - 1].InstantaneousVelocity;
                        accelerations.Add(Math.Abs(dv / dt)); // Absolute acceleration (magnitude of change)
                    }
                }

                if (accelerations.Any())
                {
                    results.MeanAcceleration = accelerations.Average();
                    results.PeakAcceleration = accelerations.Max();
                }
            }
        }
    }
}
