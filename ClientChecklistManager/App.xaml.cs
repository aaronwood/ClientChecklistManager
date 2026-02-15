using System.Windows;
using ClientChecklistManager.Data;
using ClientChecklistManager.Models;

namespace ClientChecklistManager;

public partial class App : Application
{
    public static DatabaseService Database { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Database = new DatabaseService();
        Database.Initialize();
        Settings = Database.LoadSettings();
    }

    public static void ReloadSettings()
    {
        Settings = Database.LoadSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Database?.Dispose();
        base.OnExit(e);
    }
}
