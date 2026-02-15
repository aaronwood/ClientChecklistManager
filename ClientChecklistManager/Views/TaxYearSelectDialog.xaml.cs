using System.Windows;
using ClientChecklistManager.ViewModels;

namespace ClientChecklistManager.Views;

public partial class TaxYearSelectDialog : Window
{
    private readonly TaxYearSelectViewModel _viewModel;

    public TaxYearSelectDialog(string clientName, List<int> existingYears)
    {
        InitializeComponent();
        _viewModel = new TaxYearSelectViewModel(existingYears);
        DataContext = _viewModel;
        Title = $"Select Tax Year - {clientName}";
    }

    public int SelectedTaxYear => _viewModel.SelectedTaxYear;

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
