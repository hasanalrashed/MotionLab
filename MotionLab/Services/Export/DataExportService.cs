using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MotionLab.Models;
using Microsoft.Extensions.Logging;

namespace MotionLab.Services.Export
{
    public interface IDataExportService
    {
        Task ExportToJsonAsync(TestResults results, string filePath);
        Task ExportToCsvAsync(TestResults results, string filePath);
        Task ExportSummaryCsvAsync(System.Collections.Generic.IEnumerable<TestResults> results, string filePath);
    }

    public class DataExportService : IDataExportService
    {
        private readonly ILogger<DataExportService> _logger;

        public DataExportService(ILogger<DataExportService> logger)
        {
            _logger = logger;
        }

        public async Task ExportToJsonAsync(TestResults results, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                using var stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, results, options);
                _logger.LogInformation("Successfully exported results to JSON: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export results to JSON: {FilePath}", filePath);
                throw;
            }
        }

        public async Task ExportToCsvAsync(TestResults results, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                
                // Write Metadata Header
                await writer.WriteLineAsync($"TestId,{results.TestId}");
                await writer.WriteLineAsync($"Surface,{results.Surface}");
                await writer.WriteLineAsync($"TestMode,{results.TestMode}");
                await writer.WriteLineAsync($"Duration,{results.Duration}");
                await writer.WriteLineAsync($"SampleCount,{results.SampleCount}");
                await writer.WriteLineAsync($"TotalDisplacement,{results.TotalDisplacement}");
                await writer.WriteLineAsync($"MeanVelocity,{results.MeanVelocity}");
                await writer.WriteLineAsync($"StickSlipEvents,{results.StickSlipEventCount}");
                
                await writer.WriteLineAsync(); // Empty line separator
                
                // Write Data Header
                await writer.WriteLineAsync("Timestamp(s),XDisplacement,YDisplacement,PathDisplacement,InstantaneousVelocity");
                
                // Write Data Points
                foreach (var measurement in results.Measurements)
                {
                    await writer.WriteLineAsync(
                        $"{measurement.Timestamp.TotalSeconds:F4}," +
                        $"{measurement.XDisplacement:F4}," +
                        $"{measurement.YDisplacement:F4}," +
                        $"{measurement.PathDisplacement:F4}," +
                        $"{measurement.InstantaneousVelocity:F4}");
                }

                _logger.LogInformation("Successfully exported results to CSV: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export results to CSV: {FilePath}", filePath);
                throw;
            }
        }

        public async Task ExportSummaryCsvAsync(System.Collections.Generic.IEnumerable<TestResults> results, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                
                // Write Header
                await writer.WriteLineAsync("TestId,StartTime,Surface,TestMode,Duration(s),SampleCount,TotalDisplacement(mm),MeanVelocity(mm/s),PeakVelocity(mm/s),VelocitySD,CoV(%),StickSlipEvents");
                
                // Write Data
                foreach (var r in results)
                {
                    await writer.WriteLineAsync(
                        $"{r.TestId}," +
                        $"{r.StartTime:yyyy-MM-dd HH:mm:ss}," +
                        $"{r.Surface}," +
                        $"{r.TestMode}," +
                        $"{r.Duration.TotalSeconds:F4}," +
                        $"{r.SampleCount}," +
                        $"{r.TotalDisplacement:F4}," +
                        $"{r.MeanVelocity:F4}," +
                        $"{r.PeakVelocity:F4}," +
                        $"{r.VelocityStandardDeviation:F4}," +
                        $"{r.CoefficientOfVariation:F4}," +
                        $"{r.StickSlipEventCount}");
                }

                _logger.LogInformation("Successfully exported summary to CSV: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export summary to CSV: {FilePath}", filePath);
                throw;
            }
        }
    }
}
