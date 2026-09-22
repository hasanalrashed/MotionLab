using System.Threading.Tasks;
using MotionLab.Models;

namespace MotionLab.Services.Settings
{
    public interface ISettingsService
    {
        Task<TestConfig> LoadSettingsAsync();
        Task SaveSettingsAsync(TestConfig config);
    }
}
