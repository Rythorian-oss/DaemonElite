using System.Windows;
using DaemonElite.Services;
#region SYSTEM INITIALIZATION : BLACK STAR PROJECT
/// <summary>
/// Core application node for the Black Star Research Facility.
/// </summary>
/// <remarks>
/// <code>
/// ========================================================================
///   ____  _        _    ____ _  __  ____ _____  _    ____  
///  | __ )| |      / \  / ___| |/ / / ___|_   _|/ \  |  _ \ 
///  |  _ \| |     / _ \| |   | ' /  \___ \ | | / _ \ | |_) |
///  | |_) | |___ / ___ \ |___| . \   ___) || |/ ___ \|  _ < 
///  |____/|_____/_/   \_\____|_|\_\ |____/ |_/_/   \_\_| \_\
///                                                          
///              R E S E A R C H   F A C I L I T Y           
///                                                          
///             [ LOCATION: ICELAND ]            
/// ========================================================================
/// </code>
/// </remarks>
#endregion

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
