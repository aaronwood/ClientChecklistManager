using System.Collections.ObjectModel;
using System.Windows.Input;
using ClientChecklistManager.Data;
using ClientChecklistManager.Models;

namespace ClientChecklistManager.ViewModels;

public class TemplateManagerViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private ChecklistTemplate? _selectedTemplate;

    public TemplateManagerViewModel(DatabaseService db)
    {
        _db = db;
        Templates = new ObservableCollection<ChecklistTemplate>();
        SelectedTemplateItems = new ObservableCollection<ChecklistTemplateItem>();
        DeleteTemplateCommand = new RelayCommand(_ => DeleteTemplate(), _ => SelectedTemplate != null);
        LoadTemplates();
    }

    public ObservableCollection<ChecklistTemplate> Templates { get; }
    public ObservableCollection<ChecklistTemplateItem> SelectedTemplateItems { get; }

    public ChecklistTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
                LoadTemplateItems();
        }
    }

    public int? SelectedTemplateId => SelectedTemplate?.Id;

    public ICommand DeleteTemplateCommand { get; }

    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var t in _db.GetAllTemplates())
            Templates.Add(t);
    }

    private void LoadTemplateItems()
    {
        SelectedTemplateItems.Clear();
        if (SelectedTemplate == null) return;
        foreach (var item in _db.GetTemplateItems(SelectedTemplate.Id))
            SelectedTemplateItems.Add(item);
    }

    private void DeleteTemplate()
    {
        if (SelectedTemplate == null) return;
        _db.DeleteTemplate(SelectedTemplate.Id);
        Templates.Remove(SelectedTemplate);
        SelectedTemplate = null;
    }
}
