using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace SpineForge.Models
{
    public partial class ConversionSettings : ObservableObject
    {
        [ObservableProperty]
        private string _spineExecutablePath = string.Empty;

        [ObservableProperty]
        private string _outputDirectory = string.Empty;

        [ObservableProperty]
        private string _exportSettingsPath = string.Empty;

        [ObservableProperty]
        private string _targetVersion = string.Empty;

        [ObservableProperty]
        private bool _exportJSON = true;

        [ObservableProperty]
        private bool _exportBinary = false;

        [ObservableProperty]
        private bool _packTextures = true;

        [ObservableProperty]
        private bool _cleanUp = true;

        [ObservableProperty]
        private bool _premultiplyAlpha = false;

        [ObservableProperty]
        private bool _useDefaultSettings = true;

        [ObservableProperty]
        private TextureFormat _textureFormat = TextureFormat.PNG;

        [ObservableProperty]
        private int _maxWidth = 512;
        
        [ObservableProperty]
        private int _maxHeight = 512;

        [ObservableProperty]
        private bool _resetImagePaths = true;

        [ObservableProperty]
        private bool _resetAudioPaths = true;

        // 新增：保存版本选择
        [ObservableProperty]
        private string _selectedSourceVersion = string.Empty;

        [ObservableProperty]
        private string _selectedTargetVersion = string.Empty;
        
        [ObservableProperty]
        private string _lastVersion = string.Empty;

        // 计算属性
        public string ExportSettingsPathDisplay => UseDefaultSettings 
            ? GetDefaultExportSettingsPath() 
            : ExportSettingsPath;

        public bool IsExportSettingsValid
        {
            get
            {
                if (UseDefaultSettings)
                {
                    var defaultPath = GetDefaultExportSettingsPath();
                    return !string.IsNullOrEmpty(defaultPath);
                }
                else
                {
                    return !string.IsNullOrEmpty(ExportSettingsPath) && File.Exists(ExportSettingsPath);
                }
            }
        }

        public bool IsOutputDirectoryValid
        {
            get
            {
                if (string.IsNullOrEmpty(OutputDirectory))
                    return true;
                return Directory.Exists(OutputDirectory);
            }
        }

        public ConversionSettings()
        {
            // 只初始化默认设置，不做版本检查
            // 别初始化，影响版本更新检查
            // InitializeDefaultSettings();
        }
        
        public static ConversionSettings CreateDefault()
        {
            var settings = new ConversionSettings();
            settings.InitializeDefaultSettings();
            settings.LastVersion = settings.GetCurrentVersion();
            return settings;
        }
        
        private bool CheckVersionAndResetIfNeeded()
        {
            var currentVersion = GetCurrentVersion();
            var oldVersion = LastVersion;

            // 如果是首次运行（LastVersion 为空）或者当前版本大于保存的版本
            if (string.IsNullOrEmpty(LastVersion) || IsNewerVersion(currentVersion, LastVersion))
            {
                // 重置 ExportSettingsPath 为默认路径
                ResetExportSettingsPath();

                // 更新版本号
                LastVersion = currentVersion;
        
                Console.WriteLine($"版本已更新: {oldVersion} -> {currentVersion}");
                return true; // 表示发生了变化
            }
    
            return false; // 没有变化
        }

        public bool CheckAndUpdateVersion()
        {
            return CheckVersionAndResetIfNeeded();
        }


        private bool IsNewerVersion(string currentVersion, string lastVersion)
        {
            Console.WriteLine($"版本比较: '{currentVersion}' vs '{lastVersion}'");
    
            try
            {
                // 简单粗暴但有效的方法：字符串比较
                if (string.IsNullOrEmpty(lastVersion))
                {
                    Console.WriteLine("上次版本为空，需要重置");
                    return true;
                }
        
                // 如果版本号完全相同，不需要重置
                if (currentVersion == lastVersion)
                {
                    Console.WriteLine("版本号相同，无需重置");
                    return false;
                }
        
                // 尝试 Version 对象比较
                var current = ParseVersion(currentVersion);
                var last = ParseVersion(lastVersion);
        
                var result = current > last;
                Console.WriteLine($"版本比较结果: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"版本比较失败: {ex.Message}，强制重置");
                return true;
            }
        }

        private Version ParseVersion(string versionString)
        {
            // 移除可能的前缀
            versionString = versionString?.TrimStart('v', 'V') ?? "0.0.0.0";
    
            // 如果不是标准4位格式，补齐
            var parts = versionString.Split('.');
            if (parts.Length < 4)
            {
                var newParts = new string[4];
                Array.Copy(parts, newParts, parts.Length);
                for (int i = parts.Length; i < 4; i++)
                {
                    newParts[i] = "0";
                }
                versionString = string.Join(".", newParts);
            }
    
            return new Version(versionString);
        }

        private string GetCurrentVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        }

        private void ResetExportSettingsPath()
        {
            // 重置为程序目录的默认配置路径
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = Path.Combine(appDirectory, "config", "DefaultExportSettings.json");
    
            if (File.Exists(defaultPath))
            {
                ExportSettingsPath = defaultPath;
                // 确保使用默认设置
                UseDefaultSettings = true;
            }
        }

        private void InitializeDefaultSettings()
        {
            if (UseDefaultSettings)
            {
                var defaultPath = GetDefaultExportSettingsPath();
                if (!string.IsNullOrEmpty(defaultPath))
                {
                    ExportSettingsPath = defaultPath;
                }
            }
        }

        private string GetDefaultExportSettingsPath()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = Path.Combine(appDirectory, "config", "DefaultExportSettings.json");
            return File.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        // 当相关属性改变时通知计算属性
        partial void OnOutputDirectoryChanged(string value)
        {
            OnPropertyChanged(nameof(IsOutputDirectoryValid));
        }

        partial void OnExportSettingsPathChanged(string value)
        {
            OnPropertyChanged(nameof(ExportSettingsPathDisplay));
            OnPropertyChanged(nameof(IsExportSettingsValid));
        }

        partial void OnUseDefaultSettingsChanged(bool value)
        {
            OnPropertyChanged(nameof(ExportSettingsPathDisplay));
            OnPropertyChanged(nameof(IsExportSettingsValid));
            
            if (value)
            {
                InitializeDefaultSettings();
            }
        }
    }

    public enum TextureFormat
    {
        PNG,
        JPG,
        WEBP
    }
}
