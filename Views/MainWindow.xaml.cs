using Microsoft.Extensions.DependencyInjection;
using SpineForge.Models;
using SpineForge.Services;
using SpineForge.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace SpineForge.Views;

public partial class MainWindow : FluentWindow
{
    public ConversionSettings ConversionSettings { get; }
    private MainViewModel? _viewModel;
    private Wpf.Ui.Controls.TextBox? _currentHoveredTextBox;
    private string _currentDragExtension = "";

    private readonly
        Dictionary<Wpf.Ui.Controls.TextBox, (Brush? originalBorder, Thickness originalThickness, Brush?
            originalBackground)> _originalStyles = new();

    private readonly Dictionary<Wpf.Ui.Controls.TextBlock, (Brush? originalBackground, Brush? originalForeground)>
        _originalLabelStyles = new();

    public MainWindow(MainViewModel viewModel)
    {
        ConversionSettings = new ConversionSettings();
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;

        // 启用拖拽功能
        AllowDrop = true;

        // 绑定拖拽事件到窗口
        DragEnter += MainWindow_DragEnter;
        DragOver += MainWindow_DragOver;
        DragLeave += MainWindow_DragLeave;
        Drop += MainWindow_Drop;

        // 在窗口加载完成后设置TextBox事件
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 为所有TextBox设置拖拽事件处理
        SetupTextBoxDragHandling();
    }

    private void SetupTextBoxDragHandling()
    {
        var textBoxes = FindVisualChildren<Wpf.Ui.Controls.TextBox>(this);
        foreach (var textBox in textBoxes)
        {
            // 保存原始样式
            _originalStyles[textBox] = (
                textBox.BorderBrush,
                textBox.BorderThickness,
                textBox.Background
            );

            // 启用拖拽并设置事件处理
            textBox.AllowDrop = true;

            // 使用PreviewDragOver而不是DragOver，确保我们的逻辑优先执行
            textBox.PreviewDragEnter += TextBox_PreviewDragEnter;
            textBox.PreviewDragOver += TextBox_PreviewDragOver;
            textBox.PreviewDragLeave += TextBox_PreviewDragLeave;
            textBox.PreviewDrop += TextBox_PreviewDrop;
        }
    }

    private void TextBox_PreviewDragEnter(object sender, DragEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.TextBox textBox && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var extension = Path.GetExtension(files[0]).ToLower();
                _currentDragExtension = extension;

                if (IsValidDropTarget(textBox, extension))
                {
                    e.Effects = DragDropEffects.Copy;
                    SetTextBoxHoverStyle(textBox);
                    ShowSpecificDragHint(textBox, extension, files[0]);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ShowIncompatibleHint(textBox, extension);
                }
            }
        }

        e.Handled = true; // 阻止事件继续传播
    }

    private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.TextBox textBox && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var extension = Path.GetExtension(files[0]).ToLower();

                if (IsValidDropTarget(textBox, extension))
                {
                    e.Effects = DragDropEffects.Copy;
                    // 确保样式正确应用
                    if (_currentHoveredTextBox != textBox)
                    {
                        if (_currentHoveredTextBox != null)
                        {
                            ResetTextBoxHoverStyle(_currentHoveredTextBox);
                        }

                        _currentHoveredTextBox = textBox;
                        SetTextBoxHoverStyle(textBox);
                        ShowSpecificDragHint(textBox, extension, files[0]);
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ShowIncompatibleHint(textBox, extension);
                }
            }
        }

        e.Handled = true; // 阻止事件继续传播
    }

    private void TextBox_PreviewDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.TextBox textBox)
        {
            // 检查鼠标是否真的离开了TextBox区域
            var position = e.GetPosition(textBox);
            var bounds = new Rect(0, 0, textBox.ActualWidth, textBox.ActualHeight);

            if (!bounds.Contains(position))
            {
                ResetTextBoxHoverStyle(textBox);
                if (_currentHoveredTextBox == textBox)
                {
                    _currentHoveredTextBox = null;
                }

                // 显示全局提示
                if (!string.IsNullOrEmpty(_currentDragExtension))
                {
                    ShowGlobalDragHint(_currentDragExtension);
                }
            }
        }

        e.Handled = true;
    }

    private void TextBox_PreviewDrop(object sender, DragEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.TextBox textBox && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var file = files[0];
                var extension = Path.GetExtension(file).ToLower();

                if (IsValidDropTarget(textBox, extension))
                {
                    HandleFileDrop(file, extension, textBox);
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ShowErrorMessage($"无法将 {extension} 文件拖拽到此位置");
                }
            }
        }

        // 清理状态
        HideAllDragHints();
        _currentDragExtension = "";

        e.Handled = true; // 阻止事件继续传播
    }

    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        // 只在没有TextBox处理时才执行窗口级别的逻辑
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var extension = Path.GetExtension(files[0]).ToLower();
                _currentDragExtension = extension;

                if (extension == ".exe" || extension == ".com" || extension == ".spine" || extension == ".json")
                {
                    e.Effects = DragDropEffects.Copy;
                    ShowGlobalDragHint(extension);
                    HighlightCompatibleTextBoxes(extension);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ShowGlobalDragHint("unsupported");
                }
            }
        }
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        // 窗口级别的DragOver主要用于处理非TextBox区域
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var extension = Path.GetExtension(files[0]).ToLower();

                // 检查鼠标位置是否在TextBox上
                var position = e.GetPosition(this);
                var hitElement = this.InputHitTest(position) as FrameworkElement;
                var textBox = FindParent<Wpf.Ui.Controls.TextBox>(hitElement);

                if (textBox == null)
                {
                    // 不在TextBox上，显示全局提示
                    ShowGlobalDragHint(extension);
                    e.Effects = extension == ".exe" || extension == ".com" || extension == ".spine" ||
                                extension == ".json"
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
                }
            }
        }
    }

    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        // 检查是否真的离开了窗口
        var position = e.GetPosition(this);
        var windowBounds = new Rect(0, 0, this.ActualWidth, this.ActualHeight);

        if (!windowBounds.Contains(position))
        {
            HideAllDragHints();
            _currentDragExtension = "";
        }
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        // 只在没有TextBox处理时才执行窗口级别的Drop逻辑
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var file = files[0];
                var extension = Path.GetExtension(file).ToLower();

                // 使用通用逻辑处理文件
                HandleFileDrop(file, extension, null);
            }
        }

        HideAllDragHints();
        _currentDragExtension = "";
    }

    private bool IsValidDropTarget(Wpf.Ui.Controls.TextBox textBox, string extension)
    {
        var bindingExpression = textBox.GetBindingExpression(Wpf.Ui.Controls.TextBox.TextProperty);
        if (bindingExpression?.ParentBinding?.Path?.Path == null) return false;

        var bindingPath = bindingExpression.ParentBinding.Path.Path;

        return bindingPath switch
        {
            "CurrentAsset.SpineExecutablePath" => extension == ".exe" || extension == ".com",
            "CurrentAsset.SpineFilePath" => extension == ".spine",
            "ConversionSettings.ExportSettingsPath" => extension == ".json",
            "ConversionSettings.OutputDirectory" => true,
            _ => false
        };
    }

    private void HighlightCompatibleTextBoxes(string extension)
    {
        var textBoxes = FindVisualChildren<Wpf.Ui.Controls.TextBox>(this);
        foreach (var textBox in textBoxes)
        {
            if (IsValidDropTarget(textBox, extension))
            {
                SetTextBoxCompatibleStyle(textBox);
            }
        }
    }

    private void SetTextBoxCompatibleStyle(Wpf.Ui.Controls.TextBox textBox)
    {
        // 只改变颜色，保持原有边框粗细
        textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 144, 238, 144));
        textBox.Background = new SolidColorBrush(Color.FromArgb(20, 0, 255, 0)); //
    }

    private void SetTextBoxHoverStyle(Wpf.Ui.Controls.TextBox textBox)
    {
        // 只改变颜色，保持原有边框粗细
        textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 30, 144, 255)); 
        textBox.Background = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255));
    }


    private void ResetTextBoxHoverStyle(Wpf.Ui.Controls.TextBox textBox)
    {
        if (!string.IsNullOrEmpty(_currentDragExtension) && IsValidDropTarget(textBox, _currentDragExtension))
        {
            SetTextBoxCompatibleStyle(textBox);
        }
        else
        {
            ResetTextBoxStyle(textBox);
        }
    }

    private void ResetTextBoxStyle(Wpf.Ui.Controls.TextBox textBox)
    {
        if (_originalStyles.TryGetValue(textBox, out var original))
        {
            textBox.BorderBrush = original.originalBorder;
            textBox.Background = original.originalBackground;
            // 确保边框粗细也恢复
            textBox.BorderThickness = original.originalThickness;
        }
        else
        {
            // 如果没有保存的原始样式，清除所有自定义样式
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BorderBrushProperty);
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BackgroundProperty);
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BorderThicknessProperty);
        }
    }

    private void ResetAllTextBoxStyles()
    {
        var textBoxes = FindVisualChildren<Wpf.Ui.Controls.TextBox>(this);
        foreach (var textBox in textBoxes)
        {
            ResetTextBoxStyle(textBox);
        }
    }

    private void HideAllDragHints()
    {
        if (_viewModel != null)
        {
            UpdateStatus();
        }

        _currentHoveredTextBox = null;

        // 强制重置所有TextBox样式
        var textBoxes = FindVisualChildren<Wpf.Ui.Controls.TextBox>(this);
        foreach (var textBox in textBoxes)
        {
            ResetTextBoxStyle(textBox);
        }

        // 重置所有标签样式
        foreach (var kvp in _originalLabelStyles.ToList())
        {
            var label = kvp.Key;
            var original = kvp.Value;
            label.Background = original.originalBackground;
            label.Foreground = original.originalForeground;
        }
    }

    private void HandleFileDrop(string filePath, string extension, Wpf.Ui.Controls.TextBox? targetTextBox)
    {
        if (_viewModel == null) return;

        if (targetTextBox != null)
        {
            var bindingExpression = targetTextBox.GetBindingExpression(Wpf.Ui.Controls.TextBox.TextProperty);
            if (bindingExpression?.ParentBinding?.Path?.Path != null)
            {
                var bindingPath = bindingExpression.ParentBinding.Path.Path;

                switch (bindingPath)
                {
                    case "CurrentAsset.SpineExecutablePath" when extension == ".exe" || extension == ".com":
                        _viewModel.CurrentAsset.SpineExecutablePath = filePath;
                        TriggerPropertyChanged(nameof(_viewModel.SpineExecutablePathDisplay));
                        TriggerPropertyChanged(nameof(_viewModel.CurrentAsset));
                        ShowSuccessMessage($"已设置Spine可执行文件: {Path.GetFileName(filePath)}");
                        return;

                    case "CurrentAsset.SpineFilePath" when extension == ".spine":
                        _viewModel.CurrentAsset.SpineFilePath = filePath;
                        TriggerPropertyChanged(nameof(_viewModel.SpineFilePathDisplay));
                        TriggerPropertyChanged(nameof(_viewModel.CurrentAsset));
                        ShowSuccessMessage($"已设置Spine项目文件: {Path.GetFileName(filePath)}");
                        return;

                    case "ConversionSettings.ExportSettingsPath" when extension == ".json":
                        try
                        {
                            _viewModel.ConversionSettings.ExportSettingsPath = filePath;
                            _viewModel.ConversionSettings.UseDefaultSettings = false;
                            ShowSuccessMessage($"已设置导出设置文件: {Path.GetFileName(filePath)}");
                            return;
                        }
                        catch (System.Exception ex)
                        {
                            ShowErrorMessage($"加载导出设置文件失败: {ex.Message}");
                            return;
                        }

                    case "ConversionSettings.OutputDirectory":
                        var directory = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            _viewModel.ConversionSettings.OutputDirectory = directory;
                            ShowSuccessMessage($"已设置输出目录: {directory}");
                            return;
                        }

                        break;
                }
            }
        }

        // 通用逻辑处理
        switch (extension)
        {
            case ".exe":
            case ".com":
                _viewModel.CurrentAsset.SpineExecutablePath = filePath;
                TriggerPropertyChanged(nameof(_viewModel.SpineExecutablePathDisplay));
                TriggerPropertyChanged(nameof(_viewModel.CurrentAsset));
                ShowSuccessMessage($"已设置Spine可执行文件: {Path.GetFileName(filePath)}");
                break;

            case ".spine":
                _viewModel.CurrentAsset.SpineFilePath = filePath;
                TriggerPropertyChanged(nameof(_viewModel.SpineFilePathDisplay));
                TriggerPropertyChanged(nameof(_viewModel.CurrentAsset));
                ShowSuccessMessage($"已设置Spine项目文件: {Path.GetFileName(filePath)}");
                break;

            case ".json":
                try
                {
                    _viewModel.ConversionSettings.ExportSettingsPath = filePath;
                    _viewModel.ConversionSettings.UseDefaultSettings = false;
                    ShowSuccessMessage($"已设置导出设置文件: {Path.GetFileName(filePath)}");
                }
                catch (System.Exception ex)
                {
                    ShowErrorMessage($"加载导出设置文件失败: {ex.Message}");
                }

                break;
        }

        UpdateStatus();
    }

    private void ShowGlobalDragHint(string extension)
    {
        string hint = extension switch
        {
            ".exe" or ".com" => "拖拽Spine可执行文件到绿色高亮的输入框",
            ".spine" => "拖拽Spine项目文件到绿色高亮的输入框",
            ".json" => "拖拽导出设置文件到绿色高亮的输入框",
            "unsupported" => "不支持的文件类型，支持: .exe, .com, .spine, .json",
            _ => "支持的文件类型: .exe, .com, .spine, .json"
        };

        if (_viewModel != null)
        {
            _viewModel.StatusMessage = hint;
        }
    }

    private void ShowSpecificDragHint(Wpf.Ui.Controls.TextBox textBox, string extension, string filePath)
    {
        var bindingExpression = textBox.GetBindingExpression(Wpf.Ui.Controls.TextBox.TextProperty);
        if (bindingExpression?.ParentBinding?.Path?.Path != null)
        {
            var bindingPath = bindingExpression.ParentBinding.Path.Path;

            string hint = bindingPath switch
            {
                "CurrentAsset.SpineExecutablePath" when extension == ".exe" || extension == ".com"
                    => $"松开设置Spine可执行文件: {Path.GetFileName(filePath)}",
                "CurrentAsset.SpineFilePath" when extension == ".spine"
                    => $"松开设置Spine项目文件: {Path.GetFileName(filePath)}",
                "ConversionSettings.ExportSettingsPath" when extension == ".json"
                    => $"松开设置导出设置文件: {Path.GetFileName(filePath)}",
                "ConversionSettings.OutputDirectory"
                    => $"松开设置输出目录: {Path.GetDirectoryName(filePath)}",
                _ => ""
            };

            if (!string.IsNullOrEmpty(hint) && _viewModel != null)
            {
                _viewModel.StatusMessage = hint;
            }
        }
    }

    private void ShowIncompatibleHint(Wpf.Ui.Controls.TextBox textBox, string extension)
    {
        var bindingExpression = textBox.GetBindingExpression(Wpf.Ui.Controls.TextBox.TextProperty);
        if (bindingExpression?.ParentBinding?.Path?.Path != null)
        {
            var bindingPath = bindingExpression.ParentBinding.Path.Path;

            string hint = bindingPath switch
            {
                "CurrentAsset.SpineExecutablePath" => $"此处只能拖拽 .exe 或 .com 文件，当前文件类型: {extension}",
                "CurrentAsset.SpineFilePath" => $"此处只能拖拽 .spine 文件，当前文件类型: {extension}",
                "ConversionSettings.ExportSettingsPath" => $"此处只能拖拽 .json 文件，当前文件类型: {extension}",
                "ConversionSettings.OutputDirectory" => $"可以拖拽任何文件来设置其所在目录为输出目录",
                _ => ""
            };

            if (!string.IsNullOrEmpty(hint) && _viewModel != null)
            {
                _viewModel.StatusMessage = hint;
            }
        }
    }

    // 辅助方法：查找可视化子元素
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj != null)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }

    // 辅助方法：查找父元素
    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
    }

    // 这些方法需要根据你的实际实现来添加
    private void TriggerPropertyChanged(string propertyName)
    {
        // 触发属性更改通知的实现
    }

    private void ShowSuccessMessage(string message)
    {
        // 显示成功消息的实现
        if (_viewModel != null)
        {
            _viewModel.StatusMessage = message;
        }
    }

    private void ShowErrorMessage(string message)
    {
        // 显示错误消息的实现
        if (_viewModel != null)
        {
            _viewModel.StatusMessage = message;
        }
    }

    private void UpdateStatus()
    {
        // 更新状态的实现
        if (_viewModel != null)
        {
            _viewModel.StatusMessage = "就绪";
        }
    }

    private void ForceResetAllStyles()
    {
        var textBoxes = FindVisualChildren<Wpf.Ui.Controls.TextBox>(this);
        foreach (var textBox in textBoxes)
        {
            // 强制清除所有可能的样式属性
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BorderBrushProperty);
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BackgroundProperty);
            textBox.ClearValue(Wpf.Ui.Controls.TextBox.BorderThicknessProperty);
        }

        // 清理标签样式
        var labels = FindVisualChildren<Wpf.Ui.Controls.TextBlock>(this);
        foreach (var label in labels)
        {
            label.ClearValue(Wpf.Ui.Controls.TextBlock.BackgroundProperty);
            label.ClearValue(Wpf.Ui.Controls.TextBlock.ForegroundProperty);
        }

        // 清空保存的样式
        _originalLabelStyles.Clear();
    }
}