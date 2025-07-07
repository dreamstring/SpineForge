using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
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

        // 新增：支持多文件选择
        [ObservableProperty]
        private ObservableCollection<string> _spineFilePaths = new();

        // 计算属性
        public bool IsSpineExecutableExists => !string.IsNullOrEmpty(SpineExecutablePath) && File.Exists(SpineExecutablePath);
        
        // 修改：支持多文件验证
        public bool IsSpineFileExists => 
            (!string.IsNullOrEmpty(SpineFilePath) && File.Exists(SpineFilePath)) ||
            (SpineFilePaths?.Any(path => !string.IsNullOrEmpty(path) && File.Exists(path)) == true);
        
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
                UpdateFileInfo(value);
            }
        }

        // 新增：当 SpineFilePaths 改变时的处理
        partial void OnSpineFilePathsChanged(ObservableCollection<string> value)
        {
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsSpineFileExists));
            
            // 如果有多个文件，更新主文件路径为第一个文件
            if (value?.Any() == true)
            {
                var firstValidFile = value.FirstOrDefault(path => !string.IsNullOrEmpty(path) && File.Exists(path));
                if (!string.IsNullOrEmpty(firstValidFile))
                {
                    // 避免循环调用
                    if (_spineFilePath != firstValidFile)
                    {
                        _spineFilePath = firstValidFile;
                        OnPropertyChanged(nameof(SpineFilePath));
                        UpdateFileInfo(firstValidFile);
                    }
                }
            }
        }

        // 提取文件信息更新逻辑
        private void UpdateFileInfo(string filePath)
        {
            try
            {
                FilePath = filePath;
                Name = Path.GetFileNameWithoutExtension(filePath);
                Directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                
                var fileInfo = new FileInfo(filePath);
                Size = fileInfo.Length;
                LastModified = fileInfo.LastWriteTime;
            }
            catch
            {
                // 静默处理文件信息获取错误
            }
        }

        // 构造函数中初始化集合
        public SpineAsset()
        {
            SpineFilePaths = new ObservableCollection<string>();
            
            // 监听集合变化
            SpineFilePaths.CollectionChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsSpineFileExists));
            };
        }
    }
}
