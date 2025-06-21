using Microsoft.Extensions.DependencyInjection;
using SpineForge.Models;
using SpineForge.Services;
using SpineForge.ViewModels;
using Wpf.Ui.Controls;

namespace SpineForge.Views;

public partial class MainWindow : FluentWindow
{
    public ConversionSettings ConversionSettings { get; }
    
    public MainWindow(MainViewModel viewModel)
    {
        ConversionSettings = new ConversionSettings();
        InitializeComponent();
        DataContext = viewModel;
    }
}