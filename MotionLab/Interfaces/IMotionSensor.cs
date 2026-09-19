using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using MotionLab.Models;

namespace MotionLab.Interfaces
{
    /// <summary>
    /// Abstracts the hardware motion sensor.
    /// This could be a computer mouse, a hardware encoder, or a simulated source.
    /// </summary>
    public interface IMotionSensor
    {
        /// <summary>
        /// Starts the data acquisition loop.
        /// The sensor should poll its hardware and write RawMotionDataPoint objects to the provided channel writer.
        /// </summary>
        /// <param name="writer">Channel writer to push data into the processing pipeline.</param>
        /// <param name="samplingIntervalMs">The target sampling interval in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token to gracefully stop acquisition.</param>
        /// <returns>A task representing the acquisition loop.</returns>
        Task StartAcquisitionAsync(ChannelWriter<RawMotionDataPoint> writer, int samplingIntervalMs, CancellationToken cancellationToken);
    }
}
