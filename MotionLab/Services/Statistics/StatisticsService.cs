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
            }
        }
    }
}
