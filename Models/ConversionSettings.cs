// Models/ConversionSettings.cs

using System;
using System.ComponentModel;
using System.IO;

namespace SpineForge.Models
{
    public class ConversionSettings : INotifyPropertyChanged
    {
        private string _spineExecutablePath = string.Empty;
        private string _outputDirectory = string.Empty;
        private string _exportSettingsPath = string.Empty;
        private string _targetVersion = string.Empty;
        private bool _exportJSON = true;
        private bool _exportBinary = false;
        private bool _packTextures = true;
        private bool _cleanUp = true;
        private bool _premultiplyAlpha = false;
        private bool _useDefaultSettings = true;
        private TextureFormat _textureFormat = TextureFormat.PNG;
        private int _maxWidth = 512;
        private int _maxHeight = 512;
        private bool _resetImagePaths = true;  
        private bool _resetAudioPaths = true;  

        public bool ResetImagePaths
        {
            get => _resetImagePaths;
            set
            {
                _resetImagePaths = value;
                OnPropertyChanged();
            }
        }

        public bool ResetAudioPaths
        {
            get => _resetAudioPaths;
            set
            {
                _resetAudioPaths = value;
                OnPropertyChanged();
            }
        }
        
        public int MaxWidth
        {
            get => _maxWidth;
            set
            {
                if (_maxWidth != value)
                {
                    _maxWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxHeight
        {
            get => _maxHeight;
            set
            {
                if (_maxHeight != value)
                {
                    _maxHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        // 构造函数 - 初始化默认设置路径
        public ConversionSettings()
        {
            InitializeDefaultSettings();
        }

        private void InitializeDefaultSettings()
        {
            // 如果使用默认设置，尝试找到默认配置文件
            if (_useDefaultSettings)
            {
                var defaultPath = GetDefaultExportSettingsPath();
                if (!string.IsNullOrEmpty(defaultPath))
                {
                    _exportSettingsPath = defaultPath;
                }
            }
        }

        private string GetDefaultExportSettingsPath()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = Path.Combine(appDirectory, "config", "DefaultExportSettings.json");
            
            System.Diagnostics.Debug.WriteLine($"Looking for default settings at: {defaultPath}");
            System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(defaultPath)}");
            
            return File.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        public string ExportSettingsPathDisplay
        {
            get
            {
                return UseDefaultSettings 
                    ? GetDefaultExportSettingsPath() 
                    : ExportSettingsPath;
            }
        }
        
        // 添加一个属性来检查导出设置是否有效
        public bool IsExportSettingsValid
        {
            get
            {
                if (UseDefaultSettings)
                {
                    // 检查默认设置文件是否存在
                    var defaultPath = GetDefaultExportSettingsPath();
                    return !string.IsNullOrEmpty(defaultPath);
                }
                else
                {
                    // 检查自定义设置文件是否存在
                    return !string.IsNullOrEmpty(ExportSettingsPath) && File.Exists(ExportSettingsPath);
                }
            }
        }

        public string SpineExecutablePath
        {
            get => _spineExecutablePath;
            set
            {
                _spineExecutablePath = value;
                OnPropertyChanged();
            }
        }

        public string OutputDirectory
        {
            get => _outputDirectory;
            set
            {
                _outputDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOutputDirectoryValid));
            }
        }

        public bool IsOutputDirectoryValid
        {
            get
            {
                // 空值是有效的（使用默认位置）
                if (string.IsNullOrEmpty(OutputDirectory))
                    return true;
            
                // 检查目录是否存在
                return Directory.Exists(OutputDirectory);
            }
        }


        public string ExportSettingsPath
        {
            get => _exportSettingsPath;
            set
            {
                _exportSettingsPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExportSettingsPathDisplay));
                OnPropertyChanged(nameof(IsExportSettingsValid));
            }
        }

        public bool UseDefaultSettings
        {
            get => _useDefaultSettings;
            set
            {
                _useDefaultSettings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExportSettingsPathDisplay));
                OnPropertyChanged(nameof(IsExportSettingsValid));
                
                // 当切换到使用默认设置时，重新初始化
                if (value)
                {
                    InitializeDefaultSettings();
                }
            }
        }

        public string TargetVersion
        {
            get => _targetVersion;
            set
            {
                _targetVersion = value;
                OnPropertyChanged();
            }
        }

        public bool ExportJSON
        {
            get => _exportJSON;
            set
            {
                _exportJSON = value;
                OnPropertyChanged();
            }
        }

        public bool ExportBinary
        {
            get => _exportBinary;
            set
            {
                _exportBinary = value;
                OnPropertyChanged();
            }
        }

        public bool PackTextures
        {
            get => _packTextures;
            set
            {
                _packTextures = value;
                OnPropertyChanged();
            }
        }

        public bool CleanUp
        {
            get => _cleanUp;
            set
            {
                _cleanUp = value;
                OnPropertyChanged();
            }
        }

        public bool PremultiplyAlpha
        {
            get => _premultiplyAlpha;
            set
            {
                _premultiplyAlpha = value;
                OnPropertyChanged();
            }
        }

        public TextureFormat TextureFormat
        {
            get => _textureFormat;
            set
            {
                _textureFormat = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum TextureFormat
    {
        PNG,
        JPG,
        WEBP
    }
}
