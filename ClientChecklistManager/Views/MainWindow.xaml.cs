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

        // Show tax year selection dialog
        var existingYears = App.Database.GetClientTaxYears(freshClient.Id);
        var dialog = new TaxYearSelectDialog(freshClient.Name, existingYears);
        dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;
        int selectedTaxYear = dialog.SelectedTaxYear;

        var window = new ClientDetailWindow(freshClient, selectedTaxYear);
        window.ClientUpdated += () => _viewModel.LoadClients();
        window.Owner = this;
        window.Show();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var vm = new SettingsViewModel(App.Database);
        var settingsWindow = new SettingsWindow(vm);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
        if (vm.Saved)
        {
            App.ReloadSettings();
            _viewModel.LoadClients();
        }
    }
}
