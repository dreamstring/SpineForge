using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;
using System.IO;
using System.Windows;

namespace SpineForge.Models
{
    public partial class AppSettings : ObservableObject
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpineForge",
            "app-settings.json"
        );

        // 窗口状态
        [ObservableProperty]
        private double _windowWidth = 800;

        [ObservableProperty]
        private double _windowHeight = 1050;

        [ObservableProperty]
        private double _windowLeft = (SystemParameters.PrimaryScreenWidth - 800) / 2;

        [ObservableProperty]
        private double _windowTop = (SystemParameters.PrimaryScreenHeight - 1050) / 2;

        [ObservableProperty]
        private bool _windowMaximized = false;

        // 其他应用级设置
        [ObservableProperty]
        private string _theme = "Dark"; // Light, Dark, Auto

        [ObservableProperty]
        private string _language = "zh-CN"; // 语言设置

        // 添加自动保存机制
        partial void OnWindowWidthChanged(double value) => SaveAsync();
        partial void OnWindowHeightChanged(double value) => SaveAsync();
        partial void OnWindowLeftChanged(double value) => SaveAsync();
        partial void OnWindowTopChanged(double value) => SaveAsync();
        partial void OnWindowMaximizedChanged(bool value) => SaveAsync();
        partial void OnThemeChanged(string value) => SaveAsync();
        partial void OnLanguageChanged(string value) => SaveAsync();

        // 加载设置
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }

            return new AppSettings();
        }

        // 保存设置（异步）
        public async void SaveAsync()
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        // 同步保存方法（用于应用关闭时）
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }
    }
}
