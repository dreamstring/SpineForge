using SpineForge.Models;
using System.Threading.Tasks;

namespace SpineForge.Services
{
    public interface ISettingsService
    {
        Task<SpineAsset> LoadSpineAssetAsync();
        Task SaveSpineAssetAsync(SpineAsset asset);
        Task<ConversionSettings> LoadConversionSettingsAsync();
        Task SaveConversionSettingsAsync(ConversionSettings settings);
        
        // 新增：应用设置（包含版本选择）
        Task<AppSettings> LoadAppSettingsAsync();
        Task SaveAppSettingsAsync(AppSettings settings);
    }
}