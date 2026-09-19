using MotionLab.Models;

namespace MotionLab.Services.Processing
{
    public class StickSlipDetector
    {
        private bool _isSticking = false;
        private readonly double _velocityThreshold;

        public StickSlipDetector(double velocityThreshold = 1.0)
        {
            _velocityThreshold = velocityThreshold; // Below this velocity, we consider it 'sticking'
        }

        /// <summary>
        /// Detects a stick-slip event based on current velocity.
        /// Returns true if a slip (transition from stick to slip) just occurred.
        /// </summary>
        public bool Detect(MotionMeasurement measurement)
        {
            bool isCurrentlySticking = measurement.InstantaneousVelocity < _velocityThreshold;
            bool slipDetected = false;

            if (_isSticking && !isCurrentlySticking)
            {
                // Transitioned from sticking to slipping
                slipDetected = true;
            }

            _isSticking = isCurrentlySticking;
            return slipDetected;
        }

        public void Reset()
        {
            _isSticking = false;
        }
    }
}
