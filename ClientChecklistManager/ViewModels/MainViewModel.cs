using System.Collections.ObjectModel;
using System.Windows.Input;
using ClientChecklistManager.Data;
using ClientChecklistManager.Models;

namespace ClientChecklistManager.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private string _searchText = string.Empty;
    private Client? _selectedClient;
    private string _newClientId = string.Empty;
    private string _newClientName = string.Empty;
    private string _newClientEmail = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isAddingClient;

    public MainViewModel(DatabaseService db)
    {
        _db = db;
        Clients = new ObservableCollection<Client>();

        SearchCommand = new RelayCommand(_ => Search());
        ClearSearchCommand = new RelayCommand(_ => ClearSearch());
        ShowAddClientCommand = new RelayCommand(_ => IsAddingClient = true);
        AddClientCommand = new RelayCommand(_ => AddClient());
        CancelAddClientCommand = new RelayCommand(_ => CancelAdd());
        OpenClientCommand = new RelayCommand(OpenClient);
        RefreshCommand = new RelayCommand(_ => LoadClients());

        LoadClients();
    }

    // ── Properties ──

    public ObservableCollection<Client> Clients { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                    LoadClients();
                else
                    Search();
            }
        }
    }

    public Client? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    public string NewClientId
    {
        get => _newClientId;
        set => SetProperty(ref _newClientId, value);
    }

    public string NewClientName
    {
        get => _newClientName;
        set => SetProperty(ref _newClientName, value);
    }

    public string NewClientEmail
    {
        get => _newClientEmail;
        set => SetProperty(ref _newClientEmail, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsAddingClient
    {
        get => _isAddingClient;
        set => SetProperty(ref _isAddingClient, value);
    }

    public int ClientCount => Clients.Count;

    // ── Commands ──

    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ShowAddClientCommand { get; }
    public ICommand AddClientCommand { get; }
    public ICommand CancelAddClientCommand { get; }
    public ICommand OpenClientCommand { get; }
    public ICommand RefreshCommand { get; }

    // ── Events ──

    public event Action<Client>? OpenClientRequested;

    // ── Methods ──

    public void LoadClients()
    {
        Clients.Clear();
        var clients = string.IsNullOrWhiteSpace(_searchText)
            ? _db.GetAllClients()
            : _db.SearchClients(_searchText);
        foreach (var c in clients)
            Clients.Add(c);
        OnPropertyChanged(nameof(ClientCount));
        StatusMessage = $"{Clients.Count} client(s) loaded.";
    }

    private void Search()
    {
        Clients.Clear();
        var clients = _db.SearchClients(_searchText);
        foreach (var c in clients)
            Clients.Add(c);
        OnPropertyChanged(nameof(ClientCount));
        StatusMessage = $"Found {Clients.Count} client(s).";
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void AddClient()
    {
        if (string.IsNullOrWhiteSpace(NewClientId))
        {
            StatusMessage = "Client ID is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(NewClientName))
        {
            StatusMessage = "Client Name is required.";
            return;
        }
        if (_db.ClientIdExists(NewClientId))
        {
            StatusMessage = $"Client ID \"{NewClientId}\" already exists.";
            return;
        }

        var client = new Client
        {
            ClientId = NewClientId.Trim(),
            Name = NewClientName.Trim(),
            Email = (NewClientEmail ?? "").Trim()
        };

        client.Id = _db.AddClient(client);
        Clients.Add(client);
        OnPropertyChanged(nameof(ClientCount));

        NewClientId = string.Empty;
        NewClientName = string.Empty;
        NewClientEmail = string.Empty;
        IsAddingClient = false;

        StatusMessage = $"Client \"{client.Name}\" added.";

        // Auto-open the new client with tax year selection
        OpenClientRequested?.Invoke(client);
    }

    private void CancelAdd()
    {
        NewClientId = string.Empty;
        NewClientName = string.Empty;
        NewClientEmail = string.Empty;
        IsAddingClient = false;
    }

    private void OpenClient(object? parameter)
    {
        var client = parameter as Client ?? SelectedClient;
        if (client != null)
            OpenClientRequested?.Invoke(client);
    }
}
