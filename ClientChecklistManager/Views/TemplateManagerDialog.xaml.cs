using System.Windows;
using ClientChecklistManager.ViewModels;

namespace ClientChecklistManager.Views;

public partial class TemplateManagerDialog : Window
{
    private readonly TemplateManagerViewModel _viewModel;

    public TemplateManagerDialog(TemplateManagerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public int? SelectedTemplateId => _viewModel.SelectedTemplateId;

    private void LoadSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTemplate == null)
        {
            MessageBox.Show("Please select a template first.", "No Template Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
