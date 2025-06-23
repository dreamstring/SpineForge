using Microsoft.Extensions.DependencyInjection;
using SpineForge.Services;
using SpineForge.ViewModels;
using SpineForge.Views;
using System;
using System.Text;
using System.Windows;
using Application = System.Windows.Application;

namespace SpineForge;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 设置控制台编码为 UTF-8，解决中文乱码问题
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (Exception ex)
        {
            // 如果设置编码失败，记录错误但不影响程序启动
            System.Diagnostics.Debug.WriteLine($"设置编码失败: {ex.Message}");
        }

        // 配置服务容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        _serviceProvider = services.BuildServiceProvider();

        // 获取主窗口并显示
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 注册服务
        services.AddSingleton<ISpineConverterService, SpineConverterService>();
        services.AddSingleton<ISettingsService, SettingsService>(); // 假设您也有这个接口
        
        // 注册 ViewModels
        services.AddTransient<MainViewModel>();
        
        // 注册 Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}