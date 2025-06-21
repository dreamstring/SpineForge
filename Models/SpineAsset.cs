// Models/SpineAsset.cs
using System;
using System.ComponentModel;
using System.IO;

namespace SpineForge.Models
{
    public class SpineAsset : INotifyPropertyChanged
    {
        private string? _filePath = string.Empty;
        private string _name = string.Empty;
        private string? _directory = string.Empty;
        private string _version = string.Empty;
        private long _size;
        private DateTime _lastModified;
        private bool _isSelected;
        private string _spineExecutablePath = string.Empty;
        private string? _spineFilePath = string.Empty;

        public string? FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string? Directory
        {
            get => _directory;
            set
            {
                _directory = value;
                OnPropertyChanged();
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged();
            }
        }

        public long Size
        {
            get => _size;
            set
            {
                _size = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                _lastModified = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string SpineExecutablePath
        {
            get => _spineExecutablePath;
            set
            {
                _spineExecutablePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsSpineExecutableExists));
            }
        }

        public string? SpineFilePath
        {
            get => _spineFilePath;
            set
            {
                _spineFilePath = value;
                OnPropertyChanged();
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

        // 添加缺失的验证属性
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
