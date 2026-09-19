using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MotionLab.Models;
using MotionLab.Services.Calibration;
using MotionLab.Services.Processing;
using Xunit;

namespace MotionLab.Tests.Processing
{
    public class KinematicsTests
    {
        [Fact]
        public async Task ProcessAsync_CalculatesKinematicsCorrectly()
        {
            var pipeline = new MotionProcessingPipeline(
                NullLogger<MotionProcessingPipeline>.Instance,
                new CalibrationService() // 1:1 calibration
            );

            var channel = Channel.CreateUnbounded<RawMotionDataPoint>();
            var results = new TestResults();
            var config = new TestConfig { CalibrationFactor = 1.0 };
            
            // Generate some test data
            await channel.Writer.WriteAsync(new RawMotionDataPoint(TimeSpan.FromSeconds(1), 10, 0));
            await channel.Writer.WriteAsync(new RawMotionDataPoint(TimeSpan.FromSeconds(2), 10, 0));
            channel.Writer.Complete();

            await pipeline.ProcessAsync(channel.Reader, config, results, CancellationToken.None);

            Assert.Equal(2, results.Measurements.Count);
            
            // First point: 1 second elapsed, 10 units X displacement -> 10 units path
            Assert.Equal(10, results.Measurements[0].InstantaneousVelocity);
            Assert.Equal(10, results.Measurements[0].PathDisplacement);

            // Second point: 1 second elapsed from previous, 10 units X displacement -> 20 units total path
            Assert.Equal(10, results.Measurements[1].InstantaneousVelocity);
            Assert.Equal(20, results.Measurements[1].PathDisplacement);
        }
    }
}
