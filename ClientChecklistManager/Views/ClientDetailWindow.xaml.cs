using System.Windows;
using System.Windows.Input;
using ClientChecklistManager.Models;
using ClientChecklistManager.ViewModels;

namespace ClientChecklistManager.Views;

public partial class ClientDetailWindow : Window
{
    private readonly ClientDetailViewModel _viewModel;

    public event Action? ClientUpdated;

    public ClientDetailWindow(Client client)
    {
        InitializeComponent();
        _viewModel = new ClientDetailViewModel(App.Database, client);
        _viewModel.ClientUpdated += () => ClientUpdated?.Invoke();
        _viewModel.RequestClose += () => Close();
        DataContext = _viewModel;
    }

    private void NewItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.AddItemCommand.CanExecute(null))
        {
            _viewModel.AddItemCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.OnClosing();
    }
}
