using System;
using MotionLab.Models;
using MotionLab.Services.Processing;
using Xunit;

namespace MotionLab.Tests.Processing
{
    public class StickSlipDetectorTests
    {
        [Fact]
        public void Detect_ReturnsTrueOnTransitionFromStickToSlip()
        {
            var detector = new StickSlipDetector(velocityThreshold: 5.0);

            // Initial state: not sticking (assumed or requires a stick to happen first)
            var movingFast = new MotionMeasurement(TimeSpan.FromSeconds(1), 0, 0, 0, 10.0);
            var sticking = new MotionMeasurement(TimeSpan.FromSeconds(2), 0, 0, 0, 0.0);
            var slipping = new MotionMeasurement(TimeSpan.FromSeconds(3), 0, 0, 0, 20.0);

            Assert.False(detector.Detect(movingFast), "Should not detect slip when already moving fast.");
            Assert.False(detector.Detect(sticking), "Should not detect slip when entering stick state.");
            Assert.True(detector.Detect(slipping), "Should detect slip when transitioning from stick to slip.");
        }
    }
}
