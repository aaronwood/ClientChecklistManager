using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClientChecklistManager.Data;
using ClientChecklistManager.Models;
using ClientChecklistManager.Services;

namespace ClientChecklistManager.ViewModels;

public class ClientDetailViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingChanges;

    private Client _client;
    private string _clientId;
    private string _name;
    private string _email;
    private string _newItemDescription = string.Empty;
    private string _statusMessage = string.Empty;

    public event Action? ClientUpdated;
    public event Action? RequestClose;

    public ClientDetailViewModel(DatabaseService db, Client client)
    {
        _db = db;
        _client = client;
        _clientId = client.ClientId;
        _name = client.Name;
        _email = client.Email;

        ChecklistItems = new ObservableCollection<ChecklistItemViewModel>();
        EmailLogs = new ObservableCollection<EmailLog>();

        AddItemCommand = new RelayCommand(AddItem, () => !string.IsNullOrWhiteSpace(NewItemDescription));
        DeleteItemCommand = new RelayCommand(DeleteItem);
        SendEmailCommand = new RelayCommand(SendEmail, () => OutstandingItems.Any());
        PreviewEmailCommand = new RelayCommand(PreviewEmail, () => OutstandingItems.Any());
        DeleteClientCommand = new RelayCommand(DeleteClient);

        // 450ms debounce autosave
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            SaveAll();
        };

        LoadChecklist();
        LoadEmailLogs();
    }

    // ── Properties ──

    public string ClientId
    {
        get => _clientId;
        set { if (SetProperty(ref _clientId, value)) ScheduleAutosave(); }
    }

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) ScheduleAutosave(); }
    }

    public string Email
    {
        get => _email;
        set { if (SetProperty(ref _email, value)) ScheduleAutosave(); }
    }

    public DateTime? LastEmailed => _client.LastEmailed;
    public int ClientRowId => _client.Id;

    public string NewItemDescription
    {
        get => _newItemDescription;
        set => SetProperty(ref _newItemDescription, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<ChecklistItemViewModel> ChecklistItems { get; }
    public ObservableCollection<EmailLog> EmailLogs { get; }

    public IEnumerable<ChecklistItemViewModel> OutstandingItems =>
        ChecklistItems.Where(i => !i.IsReceived);

    public int OutstandingCount => ChecklistItems.Count(i => !i.IsReceived);
    public int ReceivedCount => ChecklistItems.Count(i => i.IsReceived);
    public int TotalCount => ChecklistItems.Count;

    // ── Commands ──

    public ICommand AddItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand SendEmailCommand { get; }
    public ICommand PreviewEmailCommand { get; }
    public ICommand DeleteClientCommand { get; }

    // ── Methods ──

    private void LoadChecklist()
    {
        ChecklistItems.Clear();
        var items = _db.GetChecklistItems(_client.Id);
        foreach (var item in items)
        {
            ChecklistItems.Add(new ChecklistItemViewModel(item, OnChecklistItemChanged));
        }
        RefreshCounts();
    }

    private void LoadEmailLogs()
    {
        EmailLogs.Clear();
        var logs = _db.GetEmailLogs(_client.Id);
        foreach (var log in logs)
        {
            EmailLogs.Add(log);
        }
    }

    private void OnChecklistItemChanged()
    {
        RefreshCounts();
        ScheduleAutosave();
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(OutstandingCount));
        OnPropertyChanged(nameof(ReceivedCount));
        OnPropertyChanged(nameof(TotalCount));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ScheduleAutosave()
    {
        _hasPendingChanges = true;
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    public void SaveAll()
    {
        if (!_hasPendingChanges) return;

        try
        {
            // Validate unique ClientId
            if (_clientId != _client.ClientId && _db.ClientIdExists(_clientId, _client.Id))
            {
                StatusMessage = "Client ID already exists. Change reverted.";
                _clientId = _client.ClientId;
                OnPropertyChanged(nameof(ClientId));
                return;
            }

            _client.ClientId = _clientId;
            _client.Name = _name;
            _client.Email = _email;
            _db.UpdateClient(_client);

            foreach (var itemVm in ChecklistItems)
            {
                _db.UpdateChecklistItem(itemVm.GetModel());
            }

            _hasPendingChanges = false;
            StatusMessage = $"Saved at {DateTime.Now:h:mm:ss tt}";
            ClientUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemDescription)) return;

        var item = new ChecklistItem
        {
            ClientRowId = _client.Id,
            Description = NewItemDescription.Trim(),
            SortOrder = ChecklistItems.Count
        };

        item.Id = _db.AddChecklistItem(item);
        ChecklistItems.Add(new ChecklistItemViewModel(item, OnChecklistItemChanged));
        NewItemDescription = string.Empty;
        RefreshCounts();
        StatusMessage = "Item added.";
    }

    private void DeleteItem(object? parameter)
    {
        if (parameter is not ChecklistItemViewModel itemVm) return;

        var result = MessageBox.Show(
            $"Delete \"{itemVm.Description}\"?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _db.DeleteChecklistItem(itemVm.Id);
        ChecklistItems.Remove(itemVm);
        RefreshCounts();
        StatusMessage = "Item deleted.";
    }

    private void SendEmail()
    {
        try
        {
            SaveAll();
            var outstanding = OutstandingItems.Select(i => i.GetModel()).ToList();
            if (outstanding.Count == 0)
            {
                MessageBox.Show("No outstanding items to send.", "Email",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (subject, htmlBody) = EmailComposer.ComposeOutstandingItemsEmail(_client, outstanding);
            OutlookService.SendEmail(_client.Email, subject, htmlBody, showPreview: false);

            var now = DateTime.Now;
            _db.AddEmailLog(new EmailLog
            {
                ClientRowId = _client.Id,
                SentAt = now,
                Subject = subject,
                Body = htmlBody,
                OutstandingItemCount = outstanding.Count
            });
            _db.UpdateLastEmailed(_client.Id, now);
            _client.LastEmailed = now;
            OnPropertyChanged(nameof(LastEmailed));

            LoadEmailLogs();
            ClientUpdated?.Invoke();
            StatusMessage = $"Email sent at {now:h:mm:ss tt}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send email:\n{ex.Message}", "Email Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PreviewEmail()
    {
        try
        {
            SaveAll();
            var outstanding = OutstandingItems.Select(i => i.GetModel()).ToList();
            if (outstanding.Count == 0)
            {
                MessageBox.Show("No outstanding items to preview.", "Email Preview",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (subject, htmlBody) = EmailComposer.ComposeOutstandingItemsEmail(_client, outstanding);
            OutlookService.SendEmail(_client.Email, subject, htmlBody, showPreview: true);
            StatusMessage = "Email preview opened in Outlook.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open preview:\n{ex.Message}", "Preview Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteClient()
    {
        var result = MessageBox.Show(
            $"Permanently delete client \"{_client.Name}\" ({_client.ClientId}) and all their data?",
            "Confirm Delete Client",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _db.DeleteClient(_client.Id);
        ClientUpdated?.Invoke();
        RequestClose?.Invoke();
    }

    public void OnClosing()
    {
        _autosaveTimer.Stop();
        _hasPendingChanges = true;
        SaveAll();
    }
}
