using System.Windows;

namespace TechCheck;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "TechCheck encountered an unexpected error and could not continue.",
                "TechCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
