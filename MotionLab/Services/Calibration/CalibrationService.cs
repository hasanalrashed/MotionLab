namespace MotionLab.Services.Calibration
{
    public interface ICalibrationService
    {
        double ApplyCalibration(int rawDelta, double calibrationFactor);
    }

    public class CalibrationService : ICalibrationService
    {
        public double ApplyCalibration(int rawDelta, double calibrationFactor)
        {
            // Simple linear calibration: PhysicalDistance = RawUnits * CalibrationFactor
            return rawDelta * calibrationFactor;
        }
    }
}
