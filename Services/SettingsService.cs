using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SpineForge.Models;
using SpineForge.Services;
using SpineForge.Utils;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService>? _logger;
    private readonly string _settingsDirectory;
    private readonly string _spineAssetFile;
    private readonly string _conversionSettingsFile;
    private readonly string _appSettingsFile;

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;

        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpineForge"
        );

        _spineAssetFile = Path.Combine(_settingsDirectory, "spine-asset.json");
        _conversionSettingsFile = Path.Combine(_settingsDirectory, "conversion-settings.json");
        _appSettingsFile = Path.Combine(_settingsDirectory, "app-settings.json");

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
            var asset = JsonSerializer.Deserialize<SpineAsset>(json, JsonHelper.DefaultOptions);
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
            var json = JsonSerializer.Serialize(asset, JsonHelper.DefaultOptions);

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
                // 使用静态方法创建默认设置
                var newSettings = ConversionSettings.CreateDefault();
                return newSettings;
            }

            var json = await File.ReadAllTextAsync(_conversionSettingsFile);
            Console.WriteLine($"读取的JSON: {json}");

            var settings = JsonSerializer.Deserialize<ConversionSettings>(json, JsonHelper.DefaultOptions);
            settings ??= ConversionSettings.CreateDefault(); // 这里也改用静态方法

            Console.WriteLine($"反序列化后的LastVersion: '{settings.LastVersion}'");
    
            // 在这里进行版本检查
            bool versionChanged = settings.CheckAndUpdateVersion();

            // 如果版本发生了变化，立即保存
            if (versionChanged)
            {
                Console.WriteLine("检测到版本更新，正在保存设置...");
                await SaveConversionSettingsAsync(settings);
            }
    
            return settings;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载转换设置失败");
            return ConversionSettings.CreateDefault(); // 这里也改用静态方法
        }
    }
    
    public string GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    }

    public async Task SaveConversionSettingsAsync(ConversionSettings settings)
    {
        try
        {
            // 使用统一的序列化配置
            var json = JsonSerializer.Serialize(settings, JsonHelper.DefaultOptions);

            await File.WriteAllTextAsync(_conversionSettingsFile, json);
            Console.WriteLine($"配置已保存，LastVersion: {settings.LastVersion}");
            _logger?.LogInformation("转换设置已保存");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"配置保存失败: {ex.Message}");
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
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonHelper.DefaultOptions);
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
            var json = JsonSerializer.Serialize(settings, JsonHelper.DefaultOptions);

            await File.WriteAllTextAsync(_appSettingsFile, json);
            _logger?.LogInformation("应用设置已保存");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存应用设置失败");
        }
    }
}