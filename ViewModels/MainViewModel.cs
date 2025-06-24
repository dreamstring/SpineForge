using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpineForge.Models;
using SpineForge.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;

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

    // 用于 UI 绑定的显示属性（简化为计算属性）
    public string SpineExecutablePathDisplay =>
        string.IsNullOrEmpty(CurrentAsset?.SpineExecutablePath) ? "未选择" : CurrentAsset.SpineExecutablePath;

    public string? SpineFilePathDisplay =>
        string.IsNullOrEmpty(CurrentAsset?.SpineFilePath) ? "未选择" : CurrentAsset.SpineFilePath;

    public string? OutputDirectoryDisplay =>
        string.IsNullOrEmpty(ConversionSettings?.OutputDirectory) ? "未设置" : ConversionSettings.OutputDirectory;

    public string ExportSettingsPathDisplayProxy =>
        ConversionSettings?.ExportSettingsPathDisplay ?? "使用默认设置";

    // 验证属性
    public bool CanConvert => CurrentAsset?.IsReady == true &&
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
            OnPropertyChanged(nameof(CanConvert));
        }
        else if (e.PropertyName == nameof(CurrentAsset.SpineFilePath))
        {
            OnPropertyChanged(nameof(SpineFilePathDisplay));
            OnPropertyChanged(nameof(CanConvert));
        }
        else if (e.PropertyName == nameof(CurrentAsset.IsReady))
        {
            OnPropertyChanged(nameof(CanConvert));
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
        }
        else if (e.PropertyName == nameof(ConversionSettings.ExportSettingsPathDisplay) ||
                 e.PropertyName == nameof(ConversionSettings.UseDefaultSettings) ||
                 e.PropertyName == nameof(ConversionSettings.ExportSettingsPath))
        {
            OnPropertyChanged(nameof(ExportSettingsPathDisplayProxy));
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

        OnPropertyChanged(nameof(CanConvert));
    }

    partial void OnIsConvertingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConvert));
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
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            CurrentAsset.SpineFilePath = openFileDialog.FileName;
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

    // 其他命令方法保持不变...
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

        // 验证文件路径不为空
        if (string.IsNullOrWhiteSpace(CurrentAsset.SpineExecutablePath))
        {
            StatusMessage = "Spine 可执行文件路径不能为空";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentAsset.SpineFilePath))
        {
            StatusMessage = "Spine 项目文件路径不能为空";
            return;
        }

        // 验证文件是否存在
        if (!File.Exists(CurrentAsset.SpineExecutablePath))
        {
            StatusMessage = "Spine 可执行文件不存在";
            return;
        }

        if (!File.Exists(CurrentAsset.SpineFilePath))
        {
            StatusMessage = "Spine 项目文件不存在";
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

        var progress = new Progress<string>(message =>
        {
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
            ConversionProgress = Math.Min(ConversionProgress + 10, 90);
        });

        try
        {
            // 记录开始转换的详细信息
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 开始转换:\n";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Spine 可执行文件: {CurrentAsset.SpineExecutablePath}\n";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 源文件: {CurrentAsset.SpineFilePath}\n";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 目标版本: {SelectedTargetVersion.Version}\n";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 输出目录: {ConversionSettings.OutputDirectory}\n";

            // 创建用于转换的 SpineAsset 对象
            var assetForConversion = new SpineAsset
            {
                SpineExecutablePath = CurrentAsset.SpineExecutablePath,
                SpineFilePath = CurrentAsset.SpineFilePath,
                FilePath = CurrentAsset.SpineFilePath
            };

            // 设置转换设置中的目标版本
            ConversionSettings.TargetVersion = SelectedTargetVersion.Version;

            var success = await _converterService.ConvertAsync(assetForConversion, ConversionSettings, progress);

            ConversionProgress = 100;
            StatusMessage = success ? "转换完成!" : "转换失败，请查看日志";

            if (success)
            {
                ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 转换成功完成!\n";
                ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 输出目录: {ConversionSettings.OutputDirectory}\n";
            }

            await SaveConversionLogAsync(ConversionSettings.OutputDirectory, ConversionLog);
        }
        catch (Exception ex)
        {
            StatusMessage = $"转换过程中发生错误: {ex.Message}";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 错误: {ex.Message}\n";
            ConversionLog += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 堆栈跟踪: {ex.StackTrace}\n";
            ConversionProgress = 0;

            try
            {
                await SaveConversionLogAsync(ConversionSettings.OutputDirectory, ConversionLog);
            }
            catch
            {
                // 静默处理日志保存错误
            }
        }
        finally
        {
            IsConverting = false;
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
        try
        {
            if (string.IsNullOrWhiteSpace(logContent) || string.IsNullOrWhiteSpace(outputDir))
                return;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFileName = $"SpineForge_{timestamp}.log";
            string logFilePath = Path.Combine(outputDir, logFileName);

            string logHeader = $"SpineForge 转换日志\n";
            logHeader += $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            logHeader += $"输出目录: {outputDir}\n";
            logHeader += new string('=', 50) + "\n\n";

            string fullLogContent = logHeader + logContent;

            await File.WriteAllTextAsync(logFilePath, fullLogContent, Encoding.UTF8);
            ConversionLog += $"\n✓ 转换日志已保存到: {logFileName}";
        }
        catch (Exception ex)
        {
            ConversionLog += $"\n⚠ 保存日志文件失败: {ex.Message}";
        }
    }
}