using System.Windows;
using ClientChecklistManager.Models;
using ClientChecklistManager.ViewModels;

namespace ClientChecklistManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(App.Database);
        _viewModel.OpenClientRequested += OnOpenClientRequested;
        DataContext = _viewModel;
    }

    private void OnOpenClientRequested(Client client)
    {
        // Reload from DB to get fresh data
        var freshClient = App.Database.GetClientById(client.Id);
        if (freshClient == null) return;

        var window = new ClientDetailWindow(freshClient);
        window.ClientUpdated += () => _viewModel.LoadClients();
        window.Owner = this;
        window.Show();
    }
}
