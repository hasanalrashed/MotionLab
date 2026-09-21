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
        private bool _isCalibrating;

        [ObservableProperty]
        private string _testState = "Ready";

        [ObservableProperty]
        private TestConfig _currentConfig = new();

        [ObservableProperty]
        private TestResults _selectedTestResult;

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
            bool isRepeatability = CurrentConfig.TestMode == "Repeatability Test";

            if (isRepeatability)
            {
                TestState = "Starting in 3...";
                await Task.Delay(1000);
                TestState = "Starting in 2...";
                await Task.Delay(1000);
                TestState = "Starting in 1...";
                await Task.Delay(1000);
                TestState = "GO! Recording 5 seconds...";
            }
            else
            {
                TestState = "Running";
            }

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

            if (isRepeatability)
            {
                await Task.Delay(5000);
                if (_testCts != null && !_testCts.IsCancellationRequested)
                {
                    StopTest();
                }
            }
            else
            {
                try
                {
                    await processingTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private bool CanStartTest() => !IsCalibrating && TestState != "Running";

        [RelayCommand(CanExecute = nameof(CanStartTest))]
        private async Task StartCalibrationAsync()
        {
            IsCalibrating = true;
            StartCalibrationCommand.NotifyCanExecuteChanged();
            StartTestCommand.NotifyCanExecuteChanged();

            double oldFactor = CurrentConfig.CalibrationFactor;
            CurrentConfig.CalibrationFactor = 1.0;

            TestState = "Calibrating in 3...";
            await Task.Delay(1000);
            TestState = "Calibrating in 2...";
            await Task.Delay(1000);
            TestState = "Calibrating in 1...";
            await Task.Delay(1000);

            TestState = "GO! Move exactly 150mm!";

            _testCts = new CancellationTokenSource();
            
            CurrentResults = new TestResults
            {
                Surface = "Calibration",
                TestMode = "Auto-Calibration",
                StartTime = DateTimeOffset.Now,
                CalibrationFactor = 1.0
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

            var channel = Channel.CreateUnbounded<RawMotionDataPoint>();
            var acqTask = _sensor.StartAcquisitionAsync(channel.Writer, CurrentConfig.SamplingIntervalMs, _testCts.Token);
            var procTask = _processingPipeline.ProcessAsync(channel.Reader, CurrentConfig, CurrentResults, _testCts.Token);

            await Task.Delay(5000);

            _testCts.Cancel();
            try { await procTask; } catch (OperationCanceledException) { }

            CurrentResults.EndTime = DateTimeOffset.Now;
            CurrentResults.Duration = CurrentResults.EndTime - CurrentResults.StartTime;
            _statisticsService.CalculateFinalStatistics(CurrentResults);

            if (CurrentResults.TotalDisplacement > 0)
            {
                CurrentConfig.CalibrationFactor = 150.0 / CurrentResults.TotalDisplacement;
                TestState = $"Calibrated: Factor = {CurrentConfig.CalibrationFactor:F4}";
            }
            else
            {
                CurrentConfig.CalibrationFactor = oldFactor;
                TestState = "Calibration Failed (No movement)";
            }

            IsCalibrating = false;
            StartCalibrationCommand.NotifyCanExecuteChanged();
            StartTestCommand.NotifyCanExecuteChanged();
        }

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
                
                _statisticsService.CalculateFinalStatistics(CurrentResults);
                
                TestHistory.Add(CurrentResults);
                ClearAllTestsCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }

            TestState = "Complete";
            
            StartTestCommand.NotifyCanExecuteChanged();
            StopTestCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            
            _logger.LogInformation("Test stopped manually.");
        }

        private bool CanStopTest() => TestState == "Running" || TestState.StartsWith("GO!");

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync()
        {
            if (TestHistory == null || TestHistory.Count == 0)
            {
                MessageBox.Show("No test history to export. Please run and complete a test first.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // In a debug build, AppDomain.CurrentDomain.BaseDirectory is in bin/Debug/net8.0-windows/
                // We navigate up 4 directories to reach the project root where .gitignore lives.
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\..\..\"));
                string exportFolder = System.IO.Path.Combine(projectRoot, "ExportedData");

                if (!System.IO.Directory.Exists(exportFolder))
                {
                    System.IO.Directory.CreateDirectory(exportFolder);
                }

                foreach (var result in TestHistory)
                {
                    string baseFileName = System.IO.Path.Combine(exportFolder, $"Export_{result.TestId.ToString().Substring(0, 8)}_{result.StartTime:yyyyMMdd_HHmmss}");
                    await _exportService.ExportToCsvAsync(result, $"{baseFileName}.csv");
                    await _exportService.ExportToJsonAsync(result, $"{baseFileName}.json");
                }
                
                // Export a consolidated summary of all tests
                string summaryFileName = System.IO.Path.Combine(exportFolder, $"Summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                await _exportService.ExportSummaryCsvAsync(TestHistory, summaryFileName);
                
                MessageBox.Show($"Successfully exported {TestHistory.Count} test(s) to:\n{exportFolder}\n\nA consolidated 'Summary' CSV was also generated.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExport() => TestHistory != null && TestHistory.Count > 0;

        partial void OnSelectedTestResultChanged(TestResults value)
        {
            DeleteSelectedTestCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelectedTest))]
        private void DeleteSelectedTest()
        {
            if (SelectedTestResult != null && TestHistory.Contains(SelectedTestResult))
            {
                TestHistory.Remove(SelectedTestResult);
                ExportCommand.NotifyCanExecuteChanged();
                ClearAllTestsCommand.NotifyCanExecuteChanged();
                DeleteSelectedTestCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanDeleteSelectedTest() => SelectedTestResult != null;

        [RelayCommand(CanExecute = nameof(CanClearAllTests))]
        private void ClearAllTests()
        {
            TestHistory.Clear();
            ExportCommand.NotifyCanExecuteChanged();
            ClearAllTestsCommand.NotifyCanExecuteChanged();
            DeleteSelectedTestCommand.NotifyCanExecuteChanged();
        }

        private bool CanClearAllTests() => TestHistory != null && TestHistory.Count > 0;
    }
}
