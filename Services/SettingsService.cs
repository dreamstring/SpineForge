using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SpineForge.Models;
using SpineForge.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService>? _logger;
    private readonly string _settingsDirectory;
    private readonly string _spineAssetFile;
    private readonly string _conversionSettingsFile;
    private readonly string _appSettingsFile; // 新增

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;
        
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpineForge"
        );
        
        _spineAssetFile = Path.Combine(_settingsDirectory, "spine-asset.json");
        _conversionSettingsFile = Path.Combine(_settingsDirectory, "conversion-settings.json");
        _appSettingsFile = Path.Combine(_settingsDirectory, "app-settings.json"); // 新增
        
        Directory.CreateDirectory(_settingsDirectory);
    }

    // 新增方法
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
            _logger?.LogError(ex, "加载 Spine 资产设置失败");
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
            _logger?.LogInformation("Spine 资产设置已保存");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存 Spine 资产设置失败");
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
            _logger?.LogError(ex, "加载转换设置失败");
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
            _logger?.LogInformation("转换设置已保存");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存转换设置失败");
        }
    }

    public async Task<AppSettings> LoadAppSettingsAsync()
    {
        try
        {
            if (!File.Exists(_appSettingsFile))
            {
                return new AppSettings();
            }

            var json = await File.ReadAllTextAsync(_appSettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载应用设置失败");
            return new AppSettings();
        }
    }

    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(_appSettingsFile, json);
            _logger?.LogInformation("应用设置已保存");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存应用设置失败");
        }
    }

    // 原有方法保持不变...
}