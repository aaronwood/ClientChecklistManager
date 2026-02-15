namespace ClientChecklistManager.ViewModels;

public class TaxYearSelectViewModel : BaseViewModel
{
    private int _selectedTaxYear;

    public TaxYearSelectViewModel(List<int> existingYears)
    {
        var currentYear = DateTime.Now.Year;
        AvailableYears = Enumerable.Range(currentYear - 6, 7).Reverse().ToList();
        ExistingYears = existingYears;
        SelectedTaxYear = currentYear;
    }

    public List<int> AvailableYears { get; }
    public List<int> ExistingYears { get; }

    public int SelectedTaxYear
    {
        get => _selectedTaxYear;
        set => SetProperty(ref _selectedTaxYear, value);
    }
}
