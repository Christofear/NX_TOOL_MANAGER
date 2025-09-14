using NX_TOOL_MANAGER.Views;
using System;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // This simplified version only creates and shows the main window.
        // It does not perform any automatic library loading.
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

