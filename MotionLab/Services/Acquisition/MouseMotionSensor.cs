using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MotionLab.Interfaces;
using MotionLab.Models;

namespace MotionLab.Services.Acquisition
{
    public class MouseMotionSensor : IMotionSensor
    {
        private readonly ILogger<MouseMotionSensor> _logger;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public MouseMotionSensor(ILogger<MouseMotionSensor> logger)
        {
            _logger = logger;
        }

        public async Task StartAcquisitionAsync(ChannelWriter<RawMotionDataPoint> writer, int samplingIntervalMs, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting mouse motion acquisition. Target interval: {Interval}ms", samplingIntervalMs);

            var stopwatch = Stopwatch.StartNew();
            
            // Get initial cursor position to establish a baseline
            if (!GetCursorPos(out var previousPos))
            {
                _logger.LogWarning("Failed to get initial cursor position.");
                return;
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Delay for approximately the sampling interval.
                    // Note: Task.Delay resolution is subject to Windows scheduler (often ~15.6ms).
                    await Task.Delay(samplingIntervalMs, cancellationToken);

                    if (GetCursorPos(out var currentPos))
                    {
                        var timestamp = stopwatch.Elapsed;
                        
                        // Calculate delta (displacement since last poll)
                        int deltaX = currentPos.X - previousPos.X;
                        int deltaY = currentPos.Y - previousPos.Y;
                        
                        // We record a point even if delta is 0 to maintain accurate time-series for zero velocity.
                        var dataPoint = new RawMotionDataPoint(timestamp, deltaX, deltaY);
                        
                        if (writer.TryWrite(dataPoint))
                        {
                            previousPos = currentPos;
                        }
                        else
                        {
                            // Channel might be full or closed
                            _logger.LogWarning("Failed to write to channel. Channel may be full or closed.");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Acquisition cancelled gracefully.");
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("Mouse motion acquisition stopped.");
            }
        }
    }
}
