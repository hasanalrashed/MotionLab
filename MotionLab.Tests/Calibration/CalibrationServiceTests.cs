using MotionLab.Services.Calibration;
using Xunit;

namespace MotionLab.Tests.Calibration
{
    public class CalibrationServiceTests
    {
        [Fact]
        public void ApplyCalibration_MultipliesCorrectly()
        {
            var service = new CalibrationService();
            var result = service.ApplyCalibration(10, 2.5);
            Assert.Equal(25.0, result);
        }
    }
}
