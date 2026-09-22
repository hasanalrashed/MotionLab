using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MotionLab.Interfaces;
using MotionLab.Services.Acquisition;
using MotionLab.Services.Calibration;
using MotionLab.Services.Export;
using MotionLab.Services.Processing;
using MotionLab.Services.Statistics;
using MotionLab.Services.Settings;
using MotionLab.ViewModels;

namespace MotionLab
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            // Services
            // Depending on testing, you could swap this for SimulatedMotionSensor
            services.AddSingleton<IMotionSensor, MouseMotionSensor>();
            services.AddSingleton<ICalibrationService, CalibrationService>();
            services.AddSingleton<MotionProcessingPipeline>();
            services.AddSingleton<IStatisticsService, StatisticsService>();
            services.AddSingleton<IDataExportService, DataExportService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }
    }
}
