# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                              # Debug build
dotnet build --configuration Release      # Release build
dotnet run --project ClientChecklistManager  # Run the app
```

### Publish (Single Exe Distribution)

Always publish as a self-contained single exe:

```bash
dotnet publish ClientChecklistManager/ClientChecklistManager.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:EnableCompressionInSingleFile=true
```

- `IncludeNativeLibrariesForSelfExtract` is set in the csproj to bundle native DLLs into the single exe
- The .pdb file is not needed for distribution
- WPF does NOT support `PublishTrimmed` — do not use it

There are no tests in this project.

## Architecture

WPF desktop app (.NET 8, C# 12) using **MVVM** with manual constructor injection (no DI container).

### Project Structure

```
ClientChecklistManager/
├── App.xaml(.cs)                    # Entry point, global styles/brushes, DB init
├── Views/
│   ├── MainWindow.xaml(.cs)         # Client list with search, add, status indicators
│   ├── ClientDetailWindow.xaml(.cs) # Split pane: checklist (left) + email (right)
│   ├── SettingsWindow.xaml(.cs)     # Firm info, email config, workflow settings
│   ├── TaxYearSelectDialog.xaml(.cs)# Tax year picker (shown when opening a client)
│   ├── TemplateManagerDialog.xaml(.cs) # Two-column template browser with preview
│   └── SaveTemplateDialog.xaml(.cs) # Name input for saving checklist as template
├── ViewModels/
│   ├── BaseViewModel.cs             # INotifyPropertyChanged with SetProperty<T>
│   ├── RelayCommand.cs              # ICommand with Action + CanExecute
│   ├── MainViewModel.cs             # Client list, search, add/delete clients
│   ├── ClientDetailViewModel.cs     # Checklist CRUD, email send, auto-save, templates
│   ├── ChecklistItemViewModel.cs    # Wraps ChecklistItem for UI binding
│   ├── SettingsViewModel.cs         # Load/save AppSettings
│   ├── TaxYearSelectViewModel.cs    # Year selection with existing-year display
│   └── TemplateManagerViewModel.cs  # Template list, preview, delete
├── Models/
│   ├── Client.cs                    # Id, ClientId, FirstName, LastName, Email, LastEmailed, CreatedAt
│   ├── ChecklistItem.cs             # Description, IsReceived, ReceivedDate, Notes, SortOrder, TaxYear
│   ├── ChecklistTemplate.cs         # Id, Name, CreatedAt
│   ├── ChecklistTemplateItem.cs     # TemplateId, Description, SortOrder
│   ├── EmailLog.cs                  # Subject, Body, SentAt, OutstandingItemCount, TaxYear
│   └── AppSettings.cs               # Key-value settings (firm name, email config, font, reminder days)
├── Data/
│   └── DatabaseService.cs           # All SQLite access, singleton, schema migrations
├── Services/
│   ├── OutlookService.cs            # Static COM Interop: send email, get accounts, check availability
│   └── EmailComposer.cs             # Tag-based HTML email builder with subject template substitution
└── Converters/
    └── BoolToVisibilityConverter.cs  # Also: InverseBool, NullToVisibility, DateTimeFormat, FollowUpIndicator
```

### App Startup Flow

`App.xaml.cs` OnStartup → creates DatabaseService (auto-creates/migrates DB) → loads AppSettings → creates MainViewModel → opens MainWindow

### Database

- **Engine:** SQLite stored at `%LocalAppData%/ClientChecklistManager/clients.db`
- **Mode:** WAL mode enabled, foreign keys with cascading deletes
- **Schema version:** Tracked via `PRAGMA user_version` (current: 2), migrations in DatabaseService.RunMigrations()
- **Migration order:** RunMigrations() runs FIRST in Initialize(), before CREATE TABLE IF NOT EXISTS. This ensures existing databases get ALTER TABLE changes applied before the no-op CREATE statements. Fresh installs (no Clients table) skip migrations entirely.
- **Migration pattern:** Each version bump is wrapped in a transaction with rollback on failure. PRAGMA user_version is set outside the transaction. Add new migrations as `if (schemaVersion < N) { ... }` blocks.
- **Tables:**
  - `Clients` — core client records (FirstName, LastName, old Name kept for compat)
  - `ChecklistItems` — per-client, per-tax-year items (scoped by ClientId + TaxYear)
  - `ChecklistTemplates` + `ChecklistTemplateItems` — reusable item templates
  - `EmailLogs` — sent email history per client/year
  - `AppSettings` — key-value config store

### Key Patterns

- **Auto-save:** ClientDetailViewModel uses a 450ms DispatcherTimer debounce. Property changes → `ScheduleAutosave()` → timer fires → `SaveAll()` writes to DB.
- **Tax year scoping:** All checklist items and email logs include a TaxYear column. A client can have data across multiple years.
- **Email flow:** EmailComposer builds HTML using tag-based body template → OutlookService sends via COM Interop (GetActiveObject or new instance). Supports send-from account selection and BCC. Falls back to `mailto:` link (plain text via `ComposePreviewText()`) if classic Outlook is unavailable (e.g., new Outlook app). The mailto fallback truncates at 1500 chars due to URI length limits.
- **Email tags:** `{FirstName}`, `{LastName}`, `{ClientId}`, `{ClientEmail}`, `{TaxYear}`, `{FirmName}`, `{OutstandingItems}` — used in both subject and body templates. `{ClientName}` still supported (maps to FullName).
- **Template system:** Save current checklist as template, load templates or prior-year items into a client (with append/replace choice).
- **Follow-up indicator:** Red dot on client list when LastEmailed exceeds configurable reminder threshold (default 14 days).
- **Dialog results:** TaxYearSelectDialog, TemplateManagerDialog return results via properties after ShowDialog().
- **Client names:** Split into FirstName/LastName with computed FullName property. Sorted by LastName, FirstName. Search matches individual and combined names.

### UI Design

- **Color palette:** Primary blue (#2563EB), hover (#1D4ED8), grays (#F3F4F6, #D1D5DB)
- **Button styles:** PrimaryButton (blue), SecondaryButton (gray), DangerButton (red) — defined in App.xaml
- **Font:** 13px default in UI; email font configurable (default: Aptos 11pt, 14 font options, sizes 8-24pt)
- **Layout:** MainWindow has DataGrid + toolbar; ClientDetailWindow is split-pane (checklist | email)

## Key Dependencies

- `Microsoft.Data.Sqlite` 8.0.10 — SQLite driver
- `CommunityToolkit.Mvvm` 8.3.2 — MVVM helpers
- `Microsoft.Office.Interop.Outlook` 15.0.4797.1004 — Outlook COM Interop

## Conventions

- File-scoped namespaces (`namespace ClientChecklistManager.ViewModels;`)
- Nullable reference types enabled
- Implicit usings enabled
- Two-way data binding with `UpdateSourceTrigger=PropertyChanged`
- ObservableCollection<T> for all UI-bound lists
- Commands via RelayCommand (not CommunityToolkit's built-in — custom implementation in RelayCommand.cs)
- All DB access goes through DatabaseService (no direct SQL elsewhere)
- Views have minimal code-behind; logic lives in ViewModels

## Git

- **Main branch:** `main`
- **Repo:** https://github.com/aaronwood/ClientChecklistManager
