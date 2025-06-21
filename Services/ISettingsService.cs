using SpineForge.Models;
using System.Threading.Tasks;

namespace SpineForge.Services;

public interface ISettingsService
{
    Task<SpineAsset> LoadSpineAssetAsync();
    Task SaveSpineAssetAsync(SpineAsset asset);
    Task<ConversionSettings> LoadConversionSettingsAsync();
    Task SaveConversionSettingsAsync(ConversionSettings settings);
}