using SpineForge.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpineForge.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ILogger<SettingsService>? _logger;
        private readonly string _settingsDirectory;
        private readonly string _spineAssetFile;
        private readonly string _conversionSettingsFile;

        public SettingsService(ILogger<SettingsService>? logger = null)
        {
            _logger = logger;
            
            // 设置配置文件目录
            _settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SpineForge"
            );
            
            _spineAssetFile = Path.Combine(_settingsDirectory, "spine-asset.json");
            _conversionSettingsFile = Path.Combine(_settingsDirectory, "conversion-settings.json");
            
            // 确保目录存在
            Directory.CreateDirectory(_settingsDirectory);
        }

        public async Task<SpineAsset> LoadSpineAssetAsync()
        {
            try
            {
                if (!File.Exists(_spineAssetFile))
                {
                    return new SpineAsset();
                }

                var json = await File.ReadAllTextAsync(_spineAssetFile);
                var asset = JsonSerializer.Deserialize<SpineAsset>(json);
                return asset ?? new SpineAsset();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载 SpineAsset 设置失败");
                return new SpineAsset();
            }
        }

        public async Task SaveSpineAssetAsync(SpineAsset asset)
        {
            try
            {
                var json = JsonSerializer.Serialize(asset, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(_spineAssetFile, json);
                _logger?.LogInformation("SpineAsset 设置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存 SpineAsset 设置失败");
            }
        }

        public async Task<ConversionSettings> LoadConversionSettingsAsync()
        {
            try
            {
                if (!File.Exists(_conversionSettingsFile))
                {
                    return new ConversionSettings();
                }

                var json = await File.ReadAllTextAsync(_conversionSettingsFile);
                var settings = JsonSerializer.Deserialize<ConversionSettings>(json);
                return settings ?? new ConversionSettings();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载 ConversionSettings 设置失败");
                return new ConversionSettings();
            }
        }

        public async Task SaveConversionSettingsAsync(ConversionSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(_conversionSettingsFile, json);
                _logger?.LogInformation("ConversionSettings 设置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存 ConversionSettings 设置失败");
            }
        }
    }
}
