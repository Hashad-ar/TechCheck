using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using TechCheck.Services;
using TechCheck.ViewModels;

namespace TechCheck;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;

    private readonly MainViewModel _viewModel;
    private readonly QuickToolsService _quickTools = new();
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(
            new SystemDiagnosticsService(),
            new NetworkDiagnosticsService());
        DataContext = _viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            var enabled = 1;
            nint windowHandle = new WindowInteropHelper(this).Handle;
            _ = DwmSetWindowAttribute(
                windowHandle,
                DwmUseImmersiveDarkMode,
                ref enabled,
                Marshal.SizeOf<int>());
        }
        catch (DllNotFoundException)
        {
            // The app remains usable on Windows versions without this cosmetic API.
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) =>
        await RunCheckAsync();

    private async void RunCheckButton_Click(object sender, RoutedEventArgs e) =>
        await RunCheckAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RunCheckAsync();

    private async Task RunCheckAsync()
    {
        try
        {
            await _viewModel.RunCheckAsync();
        }
        catch (ObjectDisposedException) when (_isClosing)
        {
            // Normal window shutdown can race a queued UI event.
        }
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        string? report = _viewModel.CreateReport();
        if (report is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(report, TextDataFormat.UnicodeText);
            _viewModel.SetStatus("Report copied to clipboard");
        }
        catch (ExternalException)
        {
            ShowActionError("The clipboard is currently unavailable. Please try again.");
        }
    }

    private async void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        string? report = _viewModel.CreateReport();
        if (report is null)
        {
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Save TechCheck report",
            FileName = $"TechCheck-Report-{DateTime.Now:yyyyMMdd-HHmm}.txt",
            DefaultExt = ".txt",
            Filter = "Text files (*.txt)|*.txt",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            ValidateNames = true,
        };

        if (saveDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(saveDialog.FileName, report, new UTF8Encoding(false));
            _viewModel.SetStatus($"Report saved · {Path.GetFileName(saveDialog.FileName)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowActionError("TechCheck could not save the report at that location.");
        }
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }
            || !Enum.TryParse(tag, ignoreCase: false, out QuickTool tool))
        {
            ShowActionError("That Windows tool is unavailable.");
            return;
        }

        if (!_quickTools.TryLaunch(tool, out string? errorMessage))
        {
            ShowActionError(errorMessage ?? "Windows could not open that tool.");
            return;
        }

        _viewModel.SetStatus($"Opened {QuickToolsService.GetDisplayName(tool)}");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _viewModel.Dispose();
    }

    private void ShowActionError(string message)
    {
        _viewModel.SetStatus(message);
        MessageBox.Show(this, message, "TechCheck", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
