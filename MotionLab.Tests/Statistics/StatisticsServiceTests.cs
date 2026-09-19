using System;
using MotionLab.Models;
using MotionLab.Services.Statistics;
using Xunit;

namespace MotionLab.Tests.Statistics
{
    public class StatisticsServiceTests
    {
        [Fact]
        public void CalculateFinalStatistics_ComputesCorrectly()
        {
            var service = new StatisticsService();
            var results = new TestResults
            {
                Duration = TimeSpan.FromSeconds(2)
            };

            results.Measurements.Add(new MotionMeasurement(TimeSpan.FromSeconds(1), 0, 0, 10, 10));
            results.Measurements.Add(new MotionMeasurement(TimeSpan.FromSeconds(2), 0, 0, 20, 20));

            service.CalculateFinalStatistics(results);

            Assert.Equal(2, results.SampleCount);
            Assert.Equal(1, results.EstimatedSamplingRateHz); // 2 samples / 2 seconds
            Assert.Equal(20, results.TotalDisplacement);
            Assert.Equal(15, results.MeanVelocity);
            Assert.Equal(20, results.PeakVelocity);
            Assert.Equal(5, results.VelocityStandardDeviation);
            Assert.Equal(100.0 * (5.0 / 15.0), results.CoefficientOfVariation, 4);
        }
    }
}
