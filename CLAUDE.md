# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                              # Build the solution
dotnet build --configuration Release      # Release build
dotnet run --project ClientChecklistManager  # Run the app
dotnet publish -c Release -r win-x64      # Publish standalone executable
```

There are no tests in this project.

## Architecture

WPF desktop app (.NET 8, C# 12) using **MVVM** with manual constructor injection (no DI container).

**Layer structure:**
- **Views/** — XAML windows/dialogs with minimal code-behind
- **ViewModels/** — `BaseViewModel` (INotifyPropertyChanged) + `RelayCommand` (ICommand); each view has a corresponding ViewModel
- **Models/** — Plain data classes: Client, ChecklistItem, ChecklistTemplate, AppSettings, EmailLog
- **Data/DatabaseService.cs** — All SQLite access (~600 lines). Single service passed to ViewModels via constructor. Manages schema creation and migrations via `PRAGMA user_version`
- **Services/** — `OutlookService` (COM Interop for sending email), `EmailComposer` (static HTML email builder)
- **Converters/** — WPF value converters (BoolToVisibility, InverseBool, NullToVisibility, DateTimeFormat, FollowUpIndicator)

**App startup flow:** `App.xaml.cs` OnStartup → initializes DatabaseService → loads AppSettings → opens MainWindow.

**Database:** SQLite stored at `%LocalAppData%/ClientChecklistManager/clients.db`. WAL mode enabled, foreign keys with cascading deletes. Tables: Clients, ChecklistItems, ChecklistTemplates, ChecklistTemplateItems, EmailLogs, AppSettings.

## Key Dependencies

- `Microsoft.Data.Sqlite` 8.0.10 — SQLite driver
- `CommunityToolkit.Mvvm` 8.3.2 — MVVM helpers
- `Microsoft.Office.Interop.Outlook` 15.0.4797.1004 — Outlook COM Interop

## Conventions

- File-scoped namespaces (`namespace ClientChecklistManager.ViewModels;`)
- Nullable reference types enabled
- Implicit usings enabled
- Two-way data binding with `UpdateSourceTrigger=PropertyChanged`
- Auto-save with 450ms debounce via DispatcherTimer
- All checklist data is scoped by tax year
