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
            InitializeDefaultSettings();
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
