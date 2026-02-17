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
- **Data/DatabaseService.cs** — All SQLite access. Single service passed to ViewModels via constructor. Manages schema creation and migrations via `PRAGMA user_version`
- **Services/** — `OutlookService` (COM Interop for sending email), `EmailComposer` (tag-based HTML email builder)
- **Converters/** — WPF value converters (BoolToVisibility, InverseBool, NullToVisibility, DateTimeFormat, FollowUpIndicator)

**App startup flow:** `App.xaml.cs` OnStartup → initializes DatabaseService → loads AppSettings → opens MainWindow.

**Database:** SQLite stored at `%LocalAppData%/ClientChecklistManager/clients.db`. WAL mode enabled, foreign keys with cascading deletes. Tables: Clients, ChecklistItems, ChecklistTemplates, ChecklistTemplateItems, EmailLogs, AppSettings. Current schema version: **2** (`PRAGMA user_version`).

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

## Version 1.2 Changes (completed)

All items below are implemented and committed on branch `claude/release-v1.2-WmwiG`.

### Client name split into FirstName / LastName
- `Client` model has `FirstName`, `LastName`, and computed `FullName` property (old `Name` field kept in DB for backward compat, populated on write)
- DB migration v2 splits existing `Name` data on first space → FirstName + LastName
- All views updated: MainWindow add panel has 4 fields (Client ID, First Name, Last Name, Email); DataGrid shows LAST NAME and FIRST NAME columns; ClientDetail header/edit fields split
- Search matches FirstName, LastName, and combined full name
- Clients sorted by LastName, FirstName

### Email font settings
- `AppSettings.EmailFontFamily` (default: "Aptos") and `EmailFontSize` (default: 11pt)
- Settings view has font dropdown (14 fonts including Aptos) and size dropdown (8–24pt)
- `EmailComposer` applies font-family and font-size to HTML email `<body>` style

### Tag-based email body template
- Old `EmailHeader` / `EmailFooter` replaced by single `EmailBodyTemplate` field
- Supported tags: `{FirstName}`, `{LastName}`, `{ClientId}`, `{ClientEmail}`, `{TaxYear}`, `{FirmName}`, `{OutstandingItems}`
- `{OutstandingItems}` renders as styled HTML table in emails, numbered plain-text list in previews
- Default template demonstrates all tags (shown to first-time users)
- DB migration constructs body template from existing header/footer on upgrade
- Help text below the template field in Settings lists all available tags

### Email subject updated
- Default subject uses `{FirstName}` instead of old `{ClientName}`
- Same tags available in subject and body
- DB migration auto-replaces `{ClientName}` → `{FirstName}` in saved subject templates
- `{ClientName}` still supported in EmailComposer for backward compatibility (maps to FullName)
