using System.Windows;
using ClientChecklistManager.ViewModels;

namespace ClientChecklistManager.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public bool SettingsSaved => _viewModel.Saved;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
