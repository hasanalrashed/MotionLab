using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MotionLab.Interfaces;
using MotionLab.Models;

namespace MotionLab.Services.Acquisition
{
    /// <summary>
    /// A simulated motion sensor that generates deterministic data.
    /// Useful for running the application without relying on physical mouse movement,
    /// and for unit testing the processing pipeline.
    /// </summary>
    public class SimulatedMotionSensor : IMotionSensor
    {
        private readonly ILogger<SimulatedMotionSensor> _logger;
        private readonly Random _random;

        public SimulatedMotionSensor(ILogger<SimulatedMotionSensor> logger)
        {
            _logger = logger;
            _random = new Random(42); // deterministic seed for repeatable tests if needed
        }

        public async Task StartAcquisitionAsync(ChannelWriter<RawMotionDataPoint> writer, int samplingIntervalMs, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting simulated motion acquisition. Target interval: {Interval}ms", samplingIntervalMs);

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(samplingIntervalMs, cancellationToken);
                    
                    var timestamp = stopwatch.Elapsed;
                    
                    // Simulate some movement (e.g., constant velocity with some noise)
                    // Let's say it moves roughly 5 units per sample in X, and 0 in Y
                    int deltaX = 5 + _random.Next(-2, 3);
                    int deltaY = _random.Next(-1, 2);

                    // Add simulated stick-slip events
                    // Every ~2 seconds, stick for a bit
                    if (timestamp.TotalSeconds % 2.0 < 0.5)
                    {
                        deltaX = 0;
                        deltaY = 0;
                    }
                    else if (timestamp.TotalSeconds % 2.0 > 0.5 && timestamp.TotalSeconds % 2.0 < 0.6)
                    {
                        // Sudden slip
                        deltaX = 20;
                    }
                    
                    var dataPoint = new RawMotionDataPoint(timestamp, deltaX, deltaY);
                    
                    if (!writer.TryWrite(dataPoint))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Simulated acquisition cancelled gracefully.");
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("Simulated motion acquisition stopped.");
            }
        }
    }
}
