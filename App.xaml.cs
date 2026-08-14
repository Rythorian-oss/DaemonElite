using System.Windows;
using DaemonElite.Services;

namespace DaemonElite;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLogger.Initialize();
        AppLogger.Info("DaemonElite boot sequence started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("DaemonElite shutdown complete.");
        base.OnExit(e);
    }
}