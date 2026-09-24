using CommunityToolkit.Mvvm.ComponentModel;

namespace MotionLab.Models
{
    /// <summary>
    /// Configuration for a test run.
    /// </summary>
    public partial class TestConfig : ObservableObject
    {
        /// <summary>
        /// The target sampling interval in milliseconds (e.g., 10 for ~100Hz).
        /// Note: Actual sampling rate is subject to Windows scheduling and polling jitter.
        /// </summary>
        [ObservableProperty]
        private int _samplingIntervalMs = 10;
        
        [ObservableProperty]
        private double _calibrationFactor = 1.0;

        [ObservableProperty]
        private string _surface = "Default";

        [ObservableProperty]
        private string _testMode = "Motion Test";
        
        [ObservableProperty]
        private string _deviceName = "Default Mouse";
        
        [ObservableProperty]
        private double _autoStopDistanceMm = 0;
        
        [ObservableProperty]
        private int _smoothingWindowSize = 5;
    }
}
