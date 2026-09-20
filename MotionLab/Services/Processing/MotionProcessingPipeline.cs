using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MotionLab.Models;
using MotionLab.Services.Calibration;
using MotionLab.Services.Statistics;

namespace MotionLab.Services.Processing
{
    public class MotionProcessingPipeline
    {
        private readonly ILogger<MotionProcessingPipeline> _logger;
        private readonly ICalibrationService _calibrationService;
        private readonly StickSlipDetector _stickSlipDetector;
        
        // Expose a way for the UI to subscribe to new measurements (throttle appropriately in UI)
        public event Action<MotionMeasurement>? OnMeasurementProcessed;

        public MotionProcessingPipeline(
            ILogger<MotionProcessingPipeline> logger,
            ICalibrationService calibrationService)
        {
            _logger = logger;
            _calibrationService = calibrationService;
            _stickSlipDetector = new StickSlipDetector(velocityThreshold: 5.0); // 5 mm/s threshold for example
        }

        public async Task ProcessAsync(
            ChannelReader<RawMotionDataPoint> reader,
            TestConfig config,
            TestResults results,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting motion processing pipeline.");

            double cumulativeX = 0;
            double cumulativeY = 0;
            double cumulativePath = 0;
            
            TimeSpan previousTimestamp = TimeSpan.Zero;
            var velocityWindow = new System.Collections.Generic.Queue<double>();
            _stickSlipDetector.Reset();

            try
            {
                await foreach (var rawPoint in reader.ReadAllAsync(cancellationToken))
                {
                    // 1. Calibration
                    double deltaXPhysical = _calibrationService.ApplyCalibration(rawPoint.DeltaX, config.CalibrationFactor);
                    double deltaYPhysical = _calibrationService.ApplyCalibration(rawPoint.DeltaY, config.CalibrationFactor);
                    
                    double deltaPathPhysical = Math.Sqrt(deltaXPhysical * deltaXPhysical + deltaYPhysical * deltaYPhysical);

                    // 2. Kinematics (Accumulation & Velocity)
                    cumulativeX += deltaXPhysical;
                    cumulativeY += deltaYPhysical;
                    cumulativePath += deltaPathPhysical;

                    TimeSpan deltaTime = rawPoint.Timestamp - previousTimestamp;
                    double rawVelocity = 0;
                    
                    if (deltaTime.TotalSeconds > 0)
                    {
                        rawVelocity = deltaPathPhysical / deltaTime.TotalSeconds;
                    }

                    // Data Smoothing (Moving Average Filter)
                    int targetWindow = Math.Max(1, config.SmoothingWindowSize);
                    velocityWindow.Enqueue(rawVelocity);
                    while (velocityWindow.Count > targetWindow)
                    {
                        velocityWindow.Dequeue();
                    }

                    double instantaneousVelocity = 0;
                    foreach (var v in velocityWindow)
                    {
                        instantaneousVelocity += v;
                    }
                    if (velocityWindow.Count > 0)
                    {
                        instantaneousVelocity /= velocityWindow.Count;
                    }

                    var measurement = new MotionMeasurement(
                        rawPoint.Timestamp,
                        cumulativeX,
                        cumulativeY,
                        cumulativePath,
                        instantaneousVelocity);

                    // 3. Advanced Processing (Stick-Slip)
                    if (_stickSlipDetector.Detect(measurement))
                    {
                        results.StickSlipEventCount++;
                    }

                    // 4. Store and notify
                    results.Measurements.Add(measurement);
                    OnMeasurementProcessed?.Invoke(measurement);

                    previousTimestamp = rawPoint.Timestamp;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing pipeline cancelled gracefully.");
            }
            
            _logger.LogInformation("Processing pipeline finished.");
        }
    }
}
