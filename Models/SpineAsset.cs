using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace SpineForge.Models
{
    public partial class SpineAsset : ObservableObject
    {
        [ObservableProperty]
        private string? _filePath = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string? _directory = string.Empty;

        [ObservableProperty]
        private string _version = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _lastModified;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _spineExecutablePath = string.Empty;

        [ObservableProperty]
        private string? _spineFilePath = string.Empty;

        // 计算属性
        public bool IsSpineExecutableExists => !string.IsNullOrEmpty(SpineExecutablePath) && File.Exists(SpineExecutablePath);
        public bool IsSpineFileExists => !string.IsNullOrEmpty(SpineFilePath) && File.Exists(SpineFilePath);
        public bool IsReady => IsSpineExecutableExists && IsSpineFileExists;

        public string FormattedSize
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                else if (Size < 1024 * 1024)
                    return $"{Size / 1024:F1} KB";
                else
                    return $"{Size / (1024 * 1024):F1} MB";
            }
        }

        // 当 SpineExecutablePath 改变时，通知相关属性
        partial void OnSpineExecutablePathChanged(string value)
        {
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsSpineExecutableExists));
        }

        // 当 SpineFilePath 改变时，自动更新其他属性并通知
        partial void OnSpineFilePathChanged(string? value)
        {
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsSpineFileExists));
            
            if (!string.IsNullOrEmpty(value) && File.Exists(value))
            {
                FilePath = value;
                Name = Path.GetFileNameWithoutExtension(value);
                Directory = Path.GetDirectoryName(value) ?? string.Empty;
                
                var fileInfo = new FileInfo(value);
                Size = fileInfo.Length;
                LastModified = fileInfo.LastWriteTime;
            }
        }
    }
}
