using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MotionLab.Models;

namespace MotionLab.Services.Settings
{
    public class SettingsService : ISettingsService
    {
        private readonly ILogger<SettingsService> _logger;
        private readonly string _settingsFilePath;

        public SettingsService(ILogger<SettingsService> logger)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        public async Task<TestConfig> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    using var stream = File.OpenRead(_settingsFilePath);
                    var config = await JsonSerializer.DeserializeAsync<TestConfig>(stream);
                    if (config != null)
                    {
                        _logger.LogInformation("Successfully loaded settings from {FilePath}", _settingsFilePath);
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings from {FilePath}. Returning default configuration.", _settingsFilePath);
            }

            return new TestConfig();
        }

        public async Task SaveSettingsAsync(TestConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                using var stream = File.Create(_settingsFilePath);
                await JsonSerializer.SerializeAsync(stream, config, options);
                _logger.LogInformation("Successfully saved settings to {FilePath}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to {FilePath}", _settingsFilePath);
            }
        }
    }
}
