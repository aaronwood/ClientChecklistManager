using System.Windows.Input;
using ClientChecklistManager.Data;
using ClientChecklistManager.Models;
using ClientChecklistManager.Services;

namespace ClientChecklistManager.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private string _firmName = string.Empty;
    private string _outlookFromAccount = string.Empty;
    private string _defaultBccAddress = string.Empty;
    private string _emailSubjectTemplate = string.Empty;
    private string _emailHeader = string.Empty;
    private string _emailFooter = string.Empty;
    private int _followUpReminderDays = 14;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(DatabaseService db)
    {
        _db = db;
        SaveCommand = new RelayCommand(_ => Save());
        AvailableAccounts = new List<string> { "(Default)" };

        LoadSettings();
        LoadOutlookAccounts();
    }

    // ── Properties ──

    public string FirmName
    {
        get => _firmName;
        set => SetProperty(ref _firmName, value);
    }

    public string OutlookFromAccount
    {
        get => _outlookFromAccount;
        set => SetProperty(ref _outlookFromAccount, value);
    }

    public string DefaultBccAddress
    {
        get => _defaultBccAddress;
        set => SetProperty(ref _defaultBccAddress, value);
    }

    public string EmailSubjectTemplate
    {
        get => _emailSubjectTemplate;
        set => SetProperty(ref _emailSubjectTemplate, value);
    }

    public string EmailHeader
    {
        get => _emailHeader;
        set => SetProperty(ref _emailHeader, value);
    }

    public string EmailFooter
    {
        get => _emailFooter;
        set => SetProperty(ref _emailFooter, value);
    }

    public int FollowUpReminderDays
    {
        get => _followUpReminderDays;
        set => SetProperty(ref _followUpReminderDays, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public List<string> AvailableAccounts { get; }
    public ICommand SaveCommand { get; }
    public bool Saved { get; private set; }

    // ── Methods ──

    private void LoadSettings()
    {
        var settings = _db.LoadSettings();
        _firmName = settings.FirmName;
        _outlookFromAccount = string.IsNullOrEmpty(settings.OutlookFromAccount) ? "(Default)" : settings.OutlookFromAccount;
        _defaultBccAddress = settings.DefaultBccAddress;
        _emailSubjectTemplate = settings.EmailSubjectTemplate;
        _emailHeader = settings.EmailHeader;
        _emailFooter = settings.EmailFooter;
        _followUpReminderDays = settings.FollowUpReminderDays;
    }

    private void LoadOutlookAccounts()
    {
        try
        {
            var accounts = OutlookService.GetAccountNames();
            foreach (var account in accounts)
                AvailableAccounts.Add(account);
        }
        catch
        {
            // Outlook not available — just use default
        }
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            FirmName = FirmName ?? string.Empty,
            OutlookFromAccount = OutlookFromAccount == "(Default)" ? string.Empty : (OutlookFromAccount ?? string.Empty),
            DefaultBccAddress = DefaultBccAddress ?? string.Empty,
            EmailSubjectTemplate = EmailSubjectTemplate ?? string.Empty,
            EmailHeader = EmailHeader ?? string.Empty,
            EmailFooter = EmailFooter ?? string.Empty,
            FollowUpReminderDays = FollowUpReminderDays
        };

        _db.SaveSettings(settings);
        Saved = true;
        StatusMessage = "Settings saved.";
    }
}
