using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotionLab.Interfaces;
using MotionLab.Models;
using MotionLab.Services.Export;
using MotionLab.Services.Processing;
using MotionLab.Services.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Microsoft.Extensions.Logging;

namespace MotionLab.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IMotionSensor _sensor;
        private readonly MotionProcessingPipeline _processingPipeline;
        private readonly IStatisticsService _statisticsService;
        private readonly IDataExportService _exportService;
        private readonly ILogger<MainViewModel> _logger;

        private CancellationTokenSource? _testCts;

        [ObservableProperty]
        private string _testState = "Ready";

        [ObservableProperty]
        private TestConfig _currentConfig = new();

        [ObservableProperty]
        private TestResults? _currentResults;

        [ObservableProperty]
        private ObservableCollection<TestResults> _testHistory = new();

        // Live Measurement Properties
        [ObservableProperty]
        private double _currentDisplacement;

        [ObservableProperty]
        private double _currentVelocity;

        [ObservableProperty]
        private int _currentSampleCount;

        [ObservableProperty]
        private TimeSpan _currentDuration;

        // OxyPlot Model
        public PlotModel LivePlotModel { get; }
        private LineSeries _velocitySeries;
        private LineSeries _displacementSeries;
        
        // UI throttling
        private DateTime _lastPlotUpdate = DateTime.MinValue;
        private readonly TimeSpan _plotUpdateInterval = TimeSpan.FromMilliseconds(50); // ~20fps max for UI

        public MainViewModel(
            IMotionSensor sensor,
            MotionProcessingPipeline processingPipeline,
            IStatisticsService statisticsService,
            IDataExportService exportService,
            ILogger<MainViewModel> logger)
        {
            _sensor = sensor;
            _processingPipeline = processingPipeline;
            _statisticsService = statisticsService;
            _exportService = exportService;
            _logger = logger;

            // Setup OxyPlot
            LivePlotModel = new PlotModel { Title = "Live Kinematics" };
            
            var timeAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Time (s)", Minimum = 0 };
            var valueAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Value" };
            
            LivePlotModel.Axes.Add(timeAxis);
            LivePlotModel.Axes.Add(valueAxis);

            _velocitySeries = new LineSeries { Title = "Velocity", Color = OxyColors.Red };
            _displacementSeries = new LineSeries { Title = "Path Displacement", Color = OxyColors.Blue };

            LivePlotModel.Series.Add(_velocitySeries);
            LivePlotModel.Series.Add(_displacementSeries);

            _processingPipeline.OnMeasurementProcessed += HandleMeasurementProcessed;
        }

        private void HandleMeasurementProcessed(MotionMeasurement measurement)
        {
            // Update backing fields directly to avoid INotifyPropertyChanged overhead on background thread if possible, 
            // but we need the UI to update occasionally.
            // Using a dispatcher or throttling is safer for WPF.
            var now = DateTime.UtcNow;
            if (now - _lastPlotUpdate > _plotUpdateInterval)
            {
                _lastPlotUpdate = now;
                
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentDisplacement = measurement.PathDisplacement;
                    CurrentVelocity = measurement.InstantaneousVelocity;
                    CurrentSampleCount++;
                    CurrentDuration = measurement.Timestamp;

                    _velocitySeries.Points.Add(new DataPoint(measurement.Timestamp.TotalSeconds, measurement.InstantaneousVelocity));
                    _displacementSeries.Points.Add(new DataPoint(measurement.Timestamp.TotalSeconds, measurement.PathDisplacement));
                    
                    // Simple auto-scrolling X-axis (keep last 10 seconds visible)
                    var timeAxis = LivePlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                    if (timeAxis != null && measurement.Timestamp.TotalSeconds > 10)
                    {
                        timeAxis.Minimum = measurement.Timestamp.TotalSeconds - 10;
                        timeAxis.Maximum = measurement.Timestamp.TotalSeconds;
                    }

                    LivePlotModel.InvalidatePlot(true);
                });
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartTest))]
        private async Task StartTestAsync()
        {
            TestState = "Running";
            StartTestCommand.NotifyCanExecuteChanged();
            StopTestCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();

            _testCts = new CancellationTokenSource();
            
            CurrentResults = new TestResults
            {
                Surface = CurrentConfig.Surface,
                TestMode = CurrentConfig.TestMode,
                StartTime = DateTimeOffset.Now,
                CalibrationFactor = CurrentConfig.CalibrationFactor
            };
            
            CurrentSampleCount = 0;
            CurrentDisplacement = 0;
            CurrentVelocity = 0;
            CurrentDuration = TimeSpan.Zero;
            
            _velocitySeries.Points.Clear();
            _displacementSeries.Points.Clear();
            var timeAxis = LivePlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            if (timeAxis != null)
            {
                timeAxis.Minimum = 0;
                timeAxis.Maximum = double.NaN;
            }
            LivePlotModel.InvalidatePlot(true);

            // Create an unbounded channel for this test run
            var channel = Channel.CreateUnbounded<RawMotionDataPoint>();

            _logger.LogInformation("Test started: {TestId}", CurrentResults.TestId);

            // 1. Start Producer
            var acquisitionTask = _sensor.StartAcquisitionAsync(channel.Writer, CurrentConfig.SamplingIntervalMs, _testCts.Token);
            
            // 2. Start Consumer
            var processingTask = _processingPipeline.ProcessAsync(channel.Reader, CurrentConfig, CurrentResults, _testCts.Token);

            try
            {
                // Wait for the consumer to finish (which happens when the channel is completed or task cancelled)
                await processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when test is stopped
            }
        }

        private bool CanStartTest() => TestState == "Ready" || TestState == "Complete" || TestState == "Aborted";

        [RelayCommand(CanExecute = nameof(CanStopTest))]
        private void StopTest()
        {
            if (_testCts != null && !_testCts.IsCancellationRequested)
            {
                _testCts.Cancel();
                _testCts.Dispose();
                _testCts = null;
            }

            if (CurrentResults != null)
            {
                CurrentResults.EndTime = DateTimeOffset.Now;
                CurrentResults.Duration = CurrentResults.EndTime - CurrentResults.StartTime;
                
                // Finalize statistics calculation
                _statisticsService.CalculateFinalStatistics(CurrentResults);
                
                TestHistory.Add(CurrentResults);
            }

            TestState = "Complete";
            
            StartTestCommand.NotifyCanExecuteChanged();
            StopTestCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            
            _logger.LogInformation("Test stopped manually.");
        }

        private bool CanStopTest() => TestState == "Running";

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync()
        {
            if (CurrentResults == null) return;

            try
            {
                string exportFolder = "ExportedData";
                if (!System.IO.Directory.Exists(exportFolder))
                {
                    System.IO.Directory.CreateDirectory(exportFolder);
                }

                string baseFileName = System.IO.Path.Combine(exportFolder, $"Export_{CurrentResults.TestId.ToString().Substring(0, 8)}_{DateTime.Now:yyyyMMdd_HHmmss}");
                await _exportService.ExportToCsvAsync(CurrentResults, $"{baseFileName}.csv");
                await _exportService.ExportToJsonAsync(CurrentResults, $"{baseFileName}.json");
                
                MessageBox.Show($"Exported to {baseFileName}.csv and .json", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExport() => CurrentResults != null && (TestState == "Complete" || TestState == "Ready");
    }
}
