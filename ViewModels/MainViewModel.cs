﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpineForge.Models;
using SpineForge.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

// ReSharper disable UnusedMember.Local
// ReSharper disable LocalizableElement
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable UnusedVariable

namespace SpineForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISpineConverterService _converterService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private SpineAsset _currentAsset = new();
    [ObservableProperty] private ConversionSettings _conversionSettings = new();
    [ObservableProperty] private AppSettings _appSettings = new(); // 新增
    [ObservableProperty] private string _statusMessage = "请先选择 Spine.com 可执行文件";
    [ObservableProperty] private string _conversionLog = string.Empty;
    [ObservableProperty] private bool _isConverting = false;
    [ObservableProperty] private double _conversionProgress = 0;

    // 版本选择相关属性
    [ObservableProperty] private SpineVersion? _selectedSourceVersion;
    [ObservableProperty] private SpineVersion? _selectedTargetVersion;
    [ObservableProperty] private ObservableCollection<SpineVersion> _availableVersions = new();

    // 检测是否有中文路径
    public bool HasChinesePaths =>
        HasChinesePathInSpineFiles ||
        ConversionSettings.HasChinesePathInOutput ||
        HasChinesePathInSpineExecutable;

    public bool HasChinesePathInSpineFiles =>
        CurrentAsset?.SpineFilePaths?.Any(ContainsNonAsciiCharacters) == true;

    public bool HasChinesePathInSpineExecutable =>
        ContainsNonAsciiCharacters(CurrentAsset?.SpineExecutablePath);

    // 更新 CanStartConversion 属性
    public bool CanStartConversion =>
        !IsConverting &&
        !HasChinesePaths &&
        HasSpineFiles &&
        SelectedTargetVersion != null &&
        ConversionSettings.IsExportSettingsValid;

    public bool ButtonEnabled => CanStartConversion;

    private bool ContainsNonAsciiCharacters(string path)
    {
        return !string.IsNullOrEmpty(path) && path.Any(c => c > 127);
    }

    // 用于 UI 绑定的显示属性（简化为计算属性）
    public string SpineExecutablePathDisplay =>
        string.IsNullOrEmpty(CurrentAsset?.SpineExecutablePath) ? "未选择" : CurrentAsset.SpineExecutablePath;

    public string SpineFilePathDisplay
    {
        get
        {
            if (CurrentAsset?.SpineFilePaths == null || !CurrentAsset.SpineFilePaths.Any())
                return "未选择";

            if (CurrentAsset.SpineFilePaths.Count == 1)
                return CurrentAsset.SpineFilePaths.First();

            return $"已选择 {CurrentAsset.SpineFilePaths.Count} 个文件";
        }
    }

    public string? OutputDirectoryDisplay =>
        string.IsNullOrEmpty(ConversionSettings?.OutputDirectory) ? "未设置" : ConversionSettings.OutputDirectory;

    public string ExportSettingsPathDisplayProxy =>
        ConversionSettings?.ExportSettingsPathDisplay ?? "使用默认设置";

    // 验证属性
    public bool HasMultipleSpineFiles => CurrentAsset?.SpineFilePaths?.Count > 1;

    public bool HasSpineFiles => CurrentAsset?.SpineFilePaths?.Any() == true;

    public bool CanConvert => CurrentAsset?.IsReady == true &&
                              HasSpineFiles &&
                              SelectedTargetVersion != null &&
                              !IsConverting;

    /// <summary>
    /// 应用程序版本号
    /// </summary>
    public string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }

    /// <summary>
    /// 窗口标题（包含版本号）
    /// </summary>
    public string WindowTitle => $"SpineForge {AppVersion}";

    public MainViewModel(ISpineConverterService converterService, ISettingsService settingsService)
    {
        _converterService = converterService ?? throw new ArgumentNullException(nameof(converterService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // 订阅属性变化以自动保存
        CurrentAsset.PropertyChanged += OnCurrentAssetPropertyChanged;
        ConversionSettings.PropertyChanged += OnConversionSettingsPropertyChanged;
        AppSettings.PropertyChanged += OnAppSettingsPropertyChanged;

        // 初始化可用版本
        InitializeAvailableVersions();

        // 异步加载设置
        _ = Task.Run(async () =>
        {
            await LoadSavedSettingsAsync();
            AutoDetectSpineExecutable();
        });
    }

    // 自动保存事件处理
    private async void OnCurrentAssetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 通知相关显示属性更新
        if (e.PropertyName == nameof(CurrentAsset.SpineExecutablePath))
        {
            OnPropertyChanged(nameof(SpineExecutablePathDisplay));
            OnPropertyChanged(nameof(HasChinesePathInSpineExecutable)); // 新增
            OnPropertyChanged(nameof(HasChinesePaths)); // 新增
            OnPropertyChanged(nameof(CanStartConversion)); // 修改为使用新属性
            OnPropertyChanged(nameof(CanConvert));
            UpdateStatusMessage();
        }
        else if (e.PropertyName == nameof(CurrentAsset.SpineFilePath) ||
                 e.PropertyName == nameof(CurrentAsset.SpineFilePaths))
        {
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(HasSpineFiles));
            OnPropertyChanged(nameof(HasMultipleSpineFiles)); // 新增
            OnPropertyChanged(nameof(HasChinesePathInSpineFiles)); // 新增
            OnPropertyChanged(nameof(HasChinesePaths)); // 新增
            OnPropertyChanged(nameof(CanStartConversion)); // 修改为使用新属性
            OnPropertyChanged(nameof(CanConvert));
            UpdateStatusMessage();
        }
        else if (e.PropertyName == nameof(CurrentAsset.IsReady))
        {
            OnPropertyChanged(nameof(CanConvert));
            UpdateStatusMessage();
        }

        // 自动保存
        await SaveCurrentAssetAsync();
    }

    private async void OnConversionSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 通知相关显示属性更新
        if (e.PropertyName == nameof(ConversionSettings.OutputDirectory))
        {
            OnPropertyChanged(nameof(OutputDirectoryDisplay));
            OnPropertyChanged(nameof(HasChinesePaths)); // 新增
            OnPropertyChanged(nameof(CanStartConversion)); // 新增
            UpdateStatusMessage();
        }
        else if (e.PropertyName == nameof(ConversionSettings.HasChinesePathInOutput))
        {
            OnPropertyChanged(nameof(HasChinesePaths)); // 新增
            OnPropertyChanged(nameof(CanStartConversion)); // 新增
            UpdateStatusMessage();
        }
        else if (e.PropertyName == nameof(ConversionSettings.ExportSettingsPathDisplay) ||
                 e.PropertyName == nameof(ConversionSettings.UseDefaultSettings) ||
                 e.PropertyName == nameof(ConversionSettings.ExportSettingsPath))
        {
            OnPropertyChanged(nameof(ExportSettingsPathDisplayProxy));
            UpdateStatusMessage();
        }

        // 自动保存
        await SaveConversionSettingsAsync();
    }

    private async void OnAppSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 自动保存应用设置
        await SaveAppSettingsAsync();
    }

    // 初始化可用版本
    private void InitializeAvailableVersions()
    {
        AvailableVersions.Clear();

        var versions = new[]
        {
            new SpineVersion("4.2", "Spine 4.2"),
            new SpineVersion("4.1", "Spine 4.1"),
            new SpineVersion("4.0", "Spine 4.0"),
            new SpineVersion("3.8", "Spine 3.8"),
            new SpineVersion("3.7", "Spine 3.7"),
            new SpineVersion("3.6", "Spine 3.6")
        };

        foreach (var version in versions)
        {
            AvailableVersions.Add(version);
        }
    }

    // 版本选择变化时同步到 AppSettings 和 ConversionSettings
    partial void OnSelectedSourceVersionChanged(SpineVersion? value)
    {
        if (value != null)
        {
            ConversionSettings.SelectedSourceVersion = value.Version;
        }
    }

    partial void OnSelectedTargetVersionChanged(SpineVersion? value)
    {
        if (value != null)
        {
            ConversionSettings.SelectedTargetVersion = value.Version;
        }

        OnPropertyChanged(nameof(CanStartConversion)); // 使用新属性
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(ButtonEnabled));
        UpdateStatusMessage();
    }

    partial void OnIsConvertingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartConversion)); // 使用新属性
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(ButtonEnabled));
        UpdateStatusMessage();
    }

    // 加载保存的设置
    private async Task LoadSavedSettingsAsync()
    {
        try
        {
            // 移除旧的事件监听
            CurrentAsset.PropertyChanged -= OnCurrentAssetPropertyChanged;
            ConversionSettings.PropertyChanged -= OnConversionSettingsPropertyChanged;
            AppSettings.PropertyChanged -= OnAppSettingsPropertyChanged;

            // 加载设置
            CurrentAsset = await _settingsService.LoadSpineAssetAsync();
            ConversionSettings = await _settingsService.LoadConversionSettingsAsync();
            AppSettings = await _settingsService.LoadAppSettingsAsync();

            // 重新添加事件监听
            CurrentAsset.PropertyChanged += OnCurrentAssetPropertyChanged;
            ConversionSettings.PropertyChanged += OnConversionSettingsPropertyChanged;
            AppSettings.PropertyChanged += OnAppSettingsPropertyChanged;

            // 恢复版本选择
            if (!string.IsNullOrEmpty(ConversionSettings.SelectedSourceVersion))
            {
                SelectedSourceVersion =
                    AvailableVersions.FirstOrDefault(v => v.Version == ConversionSettings.SelectedSourceVersion);
            }

            if (!string.IsNullOrEmpty(ConversionSettings.SelectedTargetVersion))
            {
                SelectedTargetVersion =
                    AvailableVersions.FirstOrDefault(v => v.Version == ConversionSettings.SelectedTargetVersion);
            }

            // 如果没有保存的版本选择，设置默认值
            if (SelectedTargetVersion == null && AvailableVersions.Count > 0)
            {
                SelectedTargetVersion =
                    AvailableVersions.FirstOrDefault(v => v.Version == "4.1") ?? AvailableVersions[0];
            }

            if (SelectedSourceVersion == null && AvailableVersions.Count > 0)
            {
                SelectedSourceVersion =
                    AvailableVersions.FirstOrDefault(v => v.Version == "3.8") ?? AvailableVersions[0];
            }

            // 通知所有显示属性更新
            OnPropertyChanged(nameof(SpineExecutablePathDisplay));
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(OutputDirectoryDisplay));
            OnPropertyChanged(nameof(ExportSettingsPathDisplayProxy));
            OnPropertyChanged(nameof(CanConvert));

            UpdateStatusMessage();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设置时出错: {ex.Message}";
        }
    }

    // 独立的保存方法（避免在属性变化时重复保存）
    private async Task SaveCurrentAssetAsync()
    {
        try
        {
            await _settingsService.SaveSpineAssetAsync(CurrentAsset);
        }
        catch
        {
            // 静默处理保存错误
        }
    }

    private async Task SaveConversionSettingsAsync()
    {
        try
        {
            await _settingsService.SaveConversionSettingsAsync(ConversionSettings);
        }
        catch
        {
            // 静默处理保存错误
        }
    }

    private async Task SaveAppSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAppSettingsAsync(AppSettings);
        }
        catch
        {
            // 静默处理保存错误
        }
    }

    // 自动检测 Spine 可执行文件
    private void AutoDetectSpineExecutable()
    {
        if (!string.IsNullOrEmpty(CurrentAsset.SpineExecutablePath) && File.Exists(CurrentAsset.SpineExecutablePath))
        {
            return;
        }

        var defaultPaths = new string[]
        {
            @"C:\Program Files\Spine\Spine.com",
            @"C:\Program Files (x86)\Spine\Spine.com",
            "/Applications/Spine.app/Contents/MacOS/Spine"
        };

        foreach (var path in defaultPaths)
        {
            if (File.Exists(path))
            {
                CurrentAsset.SpineExecutablePath = path;
                break;
            }
        }

        UpdateStatusMessage();
    }

    // 选择 Spine 可执行文件
    [RelayCommand]
    private void SelectSpineExecutable()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择 Spine 可执行文件",
            Filter = "可执行文件 (*.exe;*.com)|*.exe;*.com|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            CurrentAsset.SpineExecutablePath = openFileDialog.FileName;
            UpdateStatusMessage();
        }
    }

    // 选择 Spine 项目文件
    [RelayCommand]
    private void SelectSpineFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择 Spine 项目文件",
            Filter = "Spine 文件 (*.spine)|*.spine|所有文件 (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            if (CurrentAsset.SpineFilePaths == null)
            {
                CurrentAsset.SpineFilePaths = new ObservableCollection<string>();
            }

            CurrentAsset.SpineFilePaths.Clear();
            foreach (var fileName in openFileDialog.FileNames)
            {
                CurrentAsset.SpineFilePaths.Add(fileName);
            }

            CurrentAsset.SpineFilePath = CurrentAsset.SpineFilePaths.FirstOrDefault() ?? string.Empty;

            // 通知UI更新
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(HasSpineFiles));
            OnPropertyChanged(nameof(HasMultipleSpineFiles)); // 新增
            OnPropertyChanged(nameof(CanConvert));

            UpdateStatusMessage();
        }
    }

    // 添加清空文件的命令
    [RelayCommand]
    private void ClearSpineFiles()
    {
        if (CurrentAsset?.SpineFilePaths != null)
        {
            CurrentAsset.SpineFilePaths.Clear();
            CurrentAsset.SpineFilePath = string.Empty;

            // 通知UI更新
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(HasSpineFiles));
            OnPropertyChanged(nameof(HasMultipleSpineFiles)); // 新增
            OnPropertyChanged(nameof(CanConvert));

            UpdateStatusMessage();
        }
    }

    [RelayCommand]
    private async void SelectExportSettings()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择导出设置文件",
            Filter = "导出设置文件 (JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ConversionSettings.ExportSettingsPath = openFileDialog.FileName;
                ConversionSettings.UseDefaultSettings = false;

                await LoadExportSettingsAsync(openFileDialog.FileName);
                StatusMessage = "导出设置文件加载成功";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载导出设置文件失败: {ex.Message}";
                ConversionSettings.ExportSettingsPath = string.Empty;
                ConversionSettings.UseDefaultSettings = true;
            }
        }
    }

    // 选择输出目录
    [RelayCommand]
    private void SelectOutputDirectory()
    {
        var folderDialog = new FolderBrowserDialog
        {
            Description = "选择输出目录",
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrEmpty(ConversionSettings.OutputDirectory))
        {
            folderDialog.SelectedPath = ConversionSettings.OutputDirectory;
        }

        if (folderDialog.ShowDialog() == DialogResult.OK)
        {
            ConversionSettings.OutputDirectory = folderDialog.SelectedPath;
        }
    }

    // 清除导出设置
    [RelayCommand]
    private void ClearExportSettings()
    {
        ConversionSettings.ExportSettingsPath = string.Empty;
        ConversionSettings.UseDefaultSettings = true;
    }

    // 清除输出目录
    [RelayCommand]
    private void ClearOutputDirectory()
    {
        ConversionSettings.OutputDirectory = string.Empty;
    }

    public void AddSpineFiles(string[] spineFiles)
    {
        if (CurrentAsset.SpineFilePaths == null)
        {
            CurrentAsset.SpineFilePaths = new ObservableCollection<string>();
        }

        foreach (var file in spineFiles)
        {
            if (!CurrentAsset.SpineFilePaths.Contains(file))
            {
                CurrentAsset.SpineFilePaths.Add(file);
            }
        }

        // 如果主文件路径为空，设置第一个文件为主文件
        if (string.IsNullOrEmpty(CurrentAsset.SpineFilePath))
        {
            CurrentAsset.SpineFilePath = CurrentAsset.SpineFilePaths.FirstOrDefault() ?? string.Empty;
        }

        // 通知UI更新
        OnPropertyChanged(nameof(SpineFilePathDisplay));
        OnPropertyChanged(nameof(HasSpineFiles));
        OnPropertyChanged(nameof(HasMultipleSpineFiles));
        OnPropertyChanged(nameof(CanConvert));

        UpdateStatusMessage();
    }

    [RelayCommand]
    private void RemoveSpineFile(string filePath)
    {
        if (CurrentAsset?.SpineFilePaths != null && CurrentAsset.SpineFilePaths.Contains(filePath))
        {
            CurrentAsset.SpineFilePaths.Remove(filePath);

            // 如果删除的是主文件，更新主文件路径
            if (CurrentAsset.SpineFilePath == filePath)
            {
                CurrentAsset.SpineFilePath = CurrentAsset.SpineFilePaths.FirstOrDefault() ?? string.Empty;
            }

            // 通知UI更新
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(HasSpineFiles));
            OnPropertyChanged(nameof(HasMultipleSpineFiles));
            OnPropertyChanged(nameof(CanConvert));

            UpdateStatusMessage();
        }
    }

    // 重置所有设置
    [RelayCommand]
    private async Task ResetSettings()
    {
        // 移除事件监听
        CurrentAsset.PropertyChanged -= OnCurrentAssetPropertyChanged;
        ConversionSettings.PropertyChanged -= OnConversionSettingsPropertyChanged;
        AppSettings.PropertyChanged -= OnAppSettingsPropertyChanged;

        // 重置对象
        CurrentAsset = new SpineAsset();
        ConversionSettings = new ConversionSettings();
        AppSettings = new AppSettings();
        SelectedSourceVersion = null;
        SelectedTargetVersion = AvailableVersions.FirstOrDefault();
        ConversionLog = string.Empty;
        ConversionProgress = 0;

        // 重新添加事件监听
        CurrentAsset.PropertyChanged += OnCurrentAssetPropertyChanged;
        ConversionSettings.PropertyChanged += OnConversionSettingsPropertyChanged;
        AppSettings.PropertyChanged += OnAppSettingsPropertyChanged;

        // 保存重置后的设置
        await SaveCurrentAssetAsync();
        await SaveConversionSettingsAsync();
        await SaveAppSettingsAsync();

        // 通知所有显示属性更新
        OnPropertyChanged(nameof(SpineExecutablePathDisplay));
        OnPropertyChanged(nameof(SpineFilePathDisplay));
        OnPropertyChanged(nameof(OutputDirectoryDisplay));
        OnPropertyChanged(nameof(ExportSettingsPathDisplayProxy));
        OnPropertyChanged(nameof(CanConvert));

        UpdateStatusMessage();
    }

    [RelayCommand]
    private async Task StartConversionAsync()
    {
        // 验证必要的输入
        if (!CurrentAsset.IsReady)
        {
            StatusMessage = "请确保已选择 Spine 可执行文件和项目文件";
            return;
        }

        if (SelectedTargetVersion == null)
        {
            StatusMessage = "请选择目标版本";
            return;
        }

        // 验证文件
        if (!HasSpineFiles)
        {
            StatusMessage = "请选择至少一个 Spine 项目文件";
            return;
        }

        // 验证所有文件是否存在
        var invalidFiles = CurrentAsset.SpineFilePaths.Where(path => string.IsNullOrEmpty(path) || !File.Exists(path))
            .ToList();
        if (invalidFiles.Any())
        {
            StatusMessage = $"以下文件不存在：{string.Join(", ", invalidFiles.Select(Path.GetFileName))}";
            return;
        }

        // 设置输出目录
        if (string.IsNullOrWhiteSpace(ConversionSettings.OutputDirectory))
        {
            try
            {
                var sourceDirectory = Path.GetDirectoryName(CurrentAsset.SpineFilePath);
                if (string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    StatusMessage = "无法确定输出目录";
                    return;
                }

                ConversionSettings.OutputDirectory = sourceDirectory;
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置输出目录时出错: {ex.Message}";
                return;
            }
        }

        // 确保输出目录存在
        try
        {
            if (!Directory.Exists(ConversionSettings.OutputDirectory))
            {
                Directory.CreateDirectory(ConversionSettings.OutputDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建输出目录失败: {ex.Message}";
            return;
        }

        IsConverting = true;
        ConversionProgress = 0;
        ConversionLog = "";
        StatusMessage = "正在转换...";

        // 创建统一的转换日志
        var unifiedLog = new StringBuilder();
        unifiedLog.AppendLine($"=== SpineForge 批量转换日志 ===");
        unifiedLog.AppendLine($"转换开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        unifiedLog.AppendLine($"文件总数: {CurrentAsset.SpineFilePaths.Count}");
        unifiedLog.AppendLine($"目标版本: {SelectedTargetVersion}");
        unifiedLog.AppendLine($"输出目录: {ConversionSettings.OutputDirectory}");
        unifiedLog.AppendLine(
            $"使用导出设置: {(ConversionSettings.UseDefaultSettings ? "默认设置" : ConversionSettings.ExportSettingsPath)}");
        unifiedLog.AppendLine(new string('=', 60));
        unifiedLog.AppendLine();

        var progress = new Progress<string>(message =>
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            ConversionLog += logEntry + "\n";
            unifiedLog.AppendLine(logEntry);
            ConversionProgress = Math.Min(ConversionProgress + 10, 90);
        });

        try
        {
            var batchStartTime = DateTime.Now;
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 开始批量转换 {CurrentAsset.SpineFilePaths.Count} 个文件:\n";
            unifiedLog.AppendLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 开始批量转换 {CurrentAsset.SpineFilePaths.Count} 个文件:");

            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < CurrentAsset.SpineFilePaths.Count; i++)
            {
                var filePath = CurrentAsset.SpineFilePaths[i];
                var fileName = Path.GetFileName(filePath);
                var fileStartTime = DateTime.Now;

                // 更新进度
                ConversionProgress = (double)(i + 1) / CurrentAsset.SpineFilePaths.Count * 90;
                StatusMessage = $"正在转换 {fileName} ({i + 1}/{CurrentAsset.SpineFilePaths.Count})";

                // 记录文件开始转换
                var startLogEntry =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 开始转换 ({i + 1}/{CurrentAsset.SpineFilePaths.Count}): {fileName}";
                ConversionLog += startLogEntry + "\n";
                unifiedLog.AppendLine();
                unifiedLog.AppendLine($"--- 文件 {i + 1}: {fileName} ---");
                unifiedLog.AppendLine($"完整路径: {filePath}");
                unifiedLog.AppendLine(startLogEntry);

                var preConversionSnapshot = TakeDirectorySnapshot(ConversionSettings.OutputDirectory);
                var snapshotLog =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 记录转换前快照，现有文件数: {preConversionSnapshot.Count}";
                unifiedLog.AppendLine(snapshotLog);

                // 为每个文件创建单独的 SpineAsset
                var assetForConversion = new SpineAsset
                {
                    SpineExecutablePath = CurrentAsset.SpineExecutablePath,
                    SpineFilePath = filePath,
                    FilePath = filePath
                };

                // 创建文件专用的进度报告器
                var fileProgress = new Progress<string>(message =>
                {
                    var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] {message}";
                    ConversionLog += logEntry + "\n";
                    unifiedLog.AppendLine(logEntry);
                });

                ConversionSettings.TargetVersion = SelectedTargetVersion.Version;
                var success =
                    await _converterService.ConvertAsync(assetForConversion, ConversionSettings, fileProgress);

                var fileEndTime = DateTime.Now;
                var fileDuration = fileEndTime - fileStartTime;

                if (success)
                {
                    successCount++;

                    // 使用快照进行文件重命名
                    await HandleFileRenamingWithSnapshotAsync(filePath, fileName, preConversionSnapshot, unifiedLog);

                    var successEntry =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ✓ 转换成功: {fileName} (耗时: {fileDuration.TotalSeconds:F1}秒)";
                    ConversionLog += successEntry + "\n";
                    unifiedLog.AppendLine(successEntry);
                }
                else
                {
                    failureCount++;
                    var failureEntry =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ✗ 转换失败: {fileName} (耗时: {fileDuration.TotalSeconds:F1}秒)";
                    ConversionLog += failureEntry + "\n";
                    unifiedLog.AppendLine(failureEntry);
                }
            }

            var batchEndTime = DateTime.Now;
            var totalDuration = batchEndTime - batchStartTime;
            var averageTime = totalDuration.TotalSeconds / CurrentAsset.SpineFilePaths.Count;

            ConversionProgress = 100;
            StatusMessage = $"批量转换完成! 成功: {successCount}, 失败: {failureCount}";

            // 添加统计信息
            unifiedLog.AppendLine();
            unifiedLog.AppendLine(new string('=', 60));
            unifiedLog.AppendLine($"=== 转换完成统计 ===");
            unifiedLog.AppendLine($"转换结束时间: {batchEndTime:yyyy-MM-dd HH:mm:ss}");
            unifiedLog.AppendLine($"总耗时: {totalDuration.TotalMinutes:F1} 分钟 ({totalDuration.TotalSeconds:F1} 秒)");
            unifiedLog.AppendLine($"成功转换: {successCount} 个文件");
            unifiedLog.AppendLine($"转换失败: {failureCount} 个文件");
            unifiedLog.AppendLine($"平均每文件耗时: {averageTime:F1} 秒");
            unifiedLog.AppendLine($"转换成功率: {(successCount * 100.0 / CurrentAsset.SpineFilePaths.Count):F1}%");

            var summaryEntry =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 批量转换完成! 成功: {successCount}, 失败: {failureCount}, 总耗时: {totalDuration.TotalMinutes:F1}分钟";
            ConversionLog += summaryEntry + "\n";

            // 只保存一个统一的日志文件
            await SaveUnifiedConversionLogAsync(ConversionSettings.OutputDirectory, unifiedLog.ToString());
        }
        catch (Exception ex)
        {
            StatusMessage = $"转换过程中发生错误: {ex.Message}";
            var errorEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 错误: {ex.Message}";
            ConversionLog += errorEntry + "\n";
            unifiedLog.AppendLine(errorEntry);
            ConversionProgress = 0;

            // 即使出错也保存日志
            await SaveUnifiedConversionLogAsync(ConversionSettings.OutputDirectory, unifiedLog.ToString());
        }
        finally
        {
            IsConverting = false;
        }
    }
    
    private HashSet<string> TakeDirectorySnapshot(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return Directory.GetFiles(directory)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出异常
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 记录目录快照时出错: {ex.Message}\n";
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // 修改后的重命名方法：使用快照比较
    private async Task HandleFileRenamingWithSnapshotAsync(string originalSpineFile, string fileName,
        HashSet<string> preConversionSnapshot, StringBuilder unifiedLog)
    {
        try
        {
            // 检查是否需要添加前缀
            if (!ConversionSettings.AddPrefix || string.IsNullOrEmpty(ConversionSettings.FilePrefix))
            {
                var skipLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 跳过重命名（未启用前缀或前缀为空）";
                unifiedLog.AppendLine(skipLog);
                return;
            }

            var outputDir = ConversionSettings.OutputDirectory;

            var logEntry =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 开始重命名文件，添加前缀: {ConversionSettings.FilePrefix}";
            ConversionLog += logEntry + "\n";
            unifiedLog.AppendLine(logEntry);

            // 找出新增的文件（转换后才有的）
            var currentFiles = Directory.GetFiles(outputDir)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newFiles = currentFiles.Except(preConversionSnapshot).ToList();

            var newJsonFiles = newFiles.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();
            var newAtlasFiles = newFiles.Where(f => f.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)).ToList();
            var newPngFiles = newFiles.Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();

            var foundFilesLog =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 找到新导出的文件: JSON({newJsonFiles.Count}), Atlas({newAtlasFiles.Count}), PNG({newPngFiles.Count})";
            ConversionLog += foundFilesLog + "\n";
            unifiedLog.AppendLine(foundFilesLog);

            // 如果没有找到新文件，记录详细信息
            if (!newFiles.Any())
            {
                var noNewFilesLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 未找到新导出的文件";
                ConversionLog += noNewFilesLog + "\n";
                unifiedLog.AppendLine(noNewFilesLog);
                unifiedLog.AppendLine(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 转换前文件数: {preConversionSnapshot.Count}, 转换后文件数: {currentFiles.Count}");
                return;
            }

            // 详细记录找到的新文件
            if (newFiles.Any())
            {
                unifiedLog.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 新增文件列表:");
                foreach (var newFile in newFiles.Take(10)) // 最多显示10个文件
                {
                    unifiedLog.AppendLine($"  - {newFile}");
                }

                if (newFiles.Count > 10)
                {
                    unifiedLog.AppendLine($"  ... 还有 {newFiles.Count - 10} 个文件");
                }
            }

            int renamedCount = 0;

            // 重命名 JSON 文件
            foreach (var jsonFileName in newJsonFiles)
            {
                var currentName = Path.GetFileNameWithoutExtension(jsonFileName);
                var newName = $"{ConversionSettings.FilePrefix}{currentName}";
                var currentPath = Path.Combine(outputDir, jsonFileName);
                var newPath = Path.Combine(outputDir, $"{newName}.json");

                if (!File.Exists(newPath) && !currentName.StartsWith(ConversionSettings.FilePrefix))
                {
                    File.Move(currentPath, newPath);
                    var renameLog =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名 JSON: {jsonFileName} -> {newName}.json";
                    ConversionLog += renameLog + "\n";
                    unifiedLog.AppendLine(renameLog);
                    renamedCount++;
                }
            }

            // 重命名 Atlas 文件
            foreach (var atlasFileName in newAtlasFiles)
            {
                var currentName = Path.GetFileNameWithoutExtension(atlasFileName);
                var newName = $"{ConversionSettings.FilePrefix}{currentName}";
                var currentPath = Path.Combine(outputDir, atlasFileName);
                var newPath = Path.Combine(outputDir, $"{newName}.atlas");

                if (!File.Exists(newPath) && !currentName.StartsWith(ConversionSettings.FilePrefix))
                {
                    File.Move(currentPath, newPath);
                    var renameLog =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名 Atlas: {atlasFileName} -> {newName}.atlas";
                    ConversionLog += renameLog + "\n";
                    unifiedLog.AppendLine(renameLog);
                    renamedCount++;
                }
            }

            var completeLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 文件重命名完成，共重命名 {renamedCount} 个文件";
            ConversionLog += completeLog + "\n";
            unifiedLog.AppendLine(completeLog);
        }
        catch (Exception ex)
        {
            var errorLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名文件时出错: {ex.Message}";
            ConversionLog += errorLog + "\n";
            unifiedLog.AppendLine(errorLog);
        }
    }

    private async Task HandleFileRenamingAsync(string originalSpineFile, string fileName, StringBuilder unifiedLog)
    {
        try
        {
            // 检查是否需要添加前缀（假设你的 ConversionSettings 有这些属性）
            if (!ConversionSettings.AddPrefix || string.IsNullOrEmpty(ConversionSettings.FilePrefix))
            {
                return; // 不需要重命名
            }

            var outputDir = ConversionSettings.OutputDirectory;
            var originalFileName = Path.GetFileNameWithoutExtension(originalSpineFile);
            var baseFileName = $"{ConversionSettings.FilePrefix}{originalFileName}";

            var logEntry =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 开始重命名文件，添加前缀: {ConversionSettings.FilePrefix}";
            ConversionLog += logEntry + "\n";
            unifiedLog.AppendLine(logEntry);

            // 重命名 JSON 文件
            var originalJsonPath = Path.Combine(outputDir, $"{originalFileName}.json");
            var newJsonPath = Path.Combine(outputDir, $"{baseFileName}.json");
            if (File.Exists(originalJsonPath))
            {
                File.Move(originalJsonPath, newJsonPath);
                var renameLog =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名 JSON: {originalFileName}.json -> {baseFileName}.json";
                ConversionLog += renameLog + "\n";
                unifiedLog.AppendLine(renameLog);
            }

            // 重命名 Atlas 文件
            var originalAtlasPath = Path.Combine(outputDir, $"{originalFileName}.atlas");
            var newAtlasPath = Path.Combine(outputDir, $"{baseFileName}.atlas");
            if (File.Exists(originalAtlasPath))
            {
                File.Move(originalAtlasPath, newAtlasPath);
                var renameLog =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名 Atlas: {originalFileName}.atlas -> {baseFileName}.atlas";
                ConversionLog += renameLog + "\n";
                unifiedLog.AppendLine(renameLog);
            }

            var completeLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 文件重命名完成";
            ConversionLog += completeLog + "\n";
            unifiedLog.AppendLine(completeLog);
        }
        catch (Exception ex)
        {
            var errorLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{fileName}] 重命名文件时出错: {ex.Message}";
            ConversionLog += errorLog + "\n";
            unifiedLog.AppendLine(errorLog);
        }
    }

    private async Task SaveUnifiedConversionLogAsync(string outputDir, string logContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logContent) || string.IsNullOrWhiteSpace(outputDir))
                return;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFileName = $"SpineForge_Conversion_{timestamp}.log";
            string logFilePath = Path.Combine(outputDir, logFileName);

            await File.WriteAllTextAsync(logFilePath, logContent, Encoding.UTF8);
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ✓ 转换日志已保存: {logFileName}\n";
        }
        catch (Exception ex)
        {
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ⚠ 保存日志失败: {ex.Message}\n";
        }
    }

    private async Task SaveFileConversionLogAsync(string outputDir, string fileNameWithoutExt, string logContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logContent) || string.IsNullOrWhiteSpace(outputDir))
                return;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            // 清理文件名中的无效字符
            string safeFileName = string.Join("_", fileNameWithoutExt.Split(Path.GetInvalidFileNameChars()));
            string logFileName = $"SpineForge_{safeFileName}_{timestamp}.log";
            string logFilePath = Path.Combine(outputDir, logFileName);

            await File.WriteAllTextAsync(logFilePath, logContent, Encoding.UTF8);
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 文件日志已保存: {logFileName}\n";
        }
        catch (Exception ex)
        {
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ⚠ 保存文件日志失败: {ex.Message}\n";
        }
    }

    private async Task SaveBatchConversionLogAsync(string outputDir, string logContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logContent) || string.IsNullOrWhiteSpace(outputDir))
                return;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFileName = $"SpineForge_BatchConversion_{timestamp}.log";
            string logFilePath = Path.Combine(outputDir, logFileName);

            string logHeader = $"SpineForge 批量转换总体日志\n";
            logHeader += $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            logHeader += $"输出目录: {outputDir}\n";
            logHeader += new string('=', 50) + "\n\n";

            string fullLogContent = logHeader + logContent;

            await File.WriteAllTextAsync(logFilePath, fullLogContent, Encoding.UTF8);
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ✓ 批量转换日志已保存: {logFileName}\n";
        }
        catch (Exception ex)
        {
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ⚠ 保存批量日志失败: {ex.Message}\n";
        }
    }

    [RelayCommand]
    private void OpenOutputDirectory()
    {
        if (!string.IsNullOrEmpty(ConversionSettings.OutputDirectory) &&
            Directory.Exists(ConversionSettings.OutputDirectory))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", ConversionSettings.OutputDirectory);
            }
            catch (Exception ex)
            {
                StatusMessage = $"无法打开输出目录: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "输出目录不存在";
        }
    }

    [RelayCommand]
    private void ClearConversionLog()
    {
        ConversionLog = string.Empty;
    }

    [RelayCommand]
    private void ValidateSettings()
    {
        var issues = new List<string>();

        if (!CurrentAsset.IsSpineExecutableExists)
            issues.Add("- Spine 可执行文件路径无效");

        if (!CurrentAsset.IsSpineFileExists)
            issues.Add("- Spine 项目文件路径无效");

        if (SelectedTargetVersion == null)
            issues.Add("- 未选择目标版本");

        if (!string.IsNullOrEmpty(ConversionSettings.ExportSettingsPath) &&
            !File.Exists(ConversionSettings.ExportSettingsPath))
            issues.Add("- 导出设置文件不存在");

        if (!string.IsNullOrEmpty(ConversionSettings.OutputDirectory) &&
            !Directory.Exists(ConversionSettings.OutputDirectory))
            issues.Add("- 输出目录不存在");

        if (issues.Count == 0)
        {
            StatusMessage = "所有设置验证通过";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 设置验证通过\n";
        }
        else
        {
            StatusMessage = "设置验证失败，请检查以下问题";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 设置验证失败:\n";
            foreach (var issue in issues)
            {
                ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {issue}\n";
            }
        }
    }

    // 私有辅助方法
    private void UpdateStatusMessage()
    {
        if (!CurrentAsset.IsSpineExecutableExists)
        {
            StatusMessage = "请先选择 Spine.com 可执行文件";
        }
        else if (!CurrentAsset.IsSpineFileExists)
        {
            StatusMessage = "请选择要转换的 Spine 项目文件";
        }
        else if (SelectedTargetVersion == null)
        {
            StatusMessage = "请选择目标版本";
        }
        else if (HasChinesePaths)
        {
            StatusMessage = "路径包含中文字符，无法转换";
        }
        else if (IsConverting)
        {
            StatusMessage = "正在转换中，请稍候...";
        }
        else
        {
            StatusMessage = "准备就绪，可以开始转换";
        }
    }

    private async Task LoadExportSettingsAsync(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException("设置文件不存在");
        }

        var settingsContent = await File.ReadAllTextAsync(settingsPath);
        ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 已加载导出设置: {settingsPath}\n";
    }

    private async Task SaveConversionLogAsync(string outputDir, string logContent)
    {
        // 这个方法现在可以调用 SaveBatchConversionLogAsync
        await SaveBatchConversionLogAsync(outputDir, logContent);
    }
}