using System.Windows;
using ClientChecklistManager.Data;

namespace ClientChecklistManager;

public partial class App : Application
{
    public static DatabaseService Database { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Database = new DatabaseService();
        Database.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Database?.Dispose();
        base.OnExit(e);
    }
}
