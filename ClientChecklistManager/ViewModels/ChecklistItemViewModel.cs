using ClientChecklistManager.Models;

namespace ClientChecklistManager.ViewModels;

public class ChecklistItemViewModel : BaseViewModel
{
    private readonly ChecklistItem _model;
    private readonly Action _onChanged;

    public ChecklistItemViewModel(ChecklistItem model, Action onChanged)
    {
        _model = model;
        _onChanged = onChanged;
    }

    public int Id => _model.Id;
    public int ClientRowId => _model.ClientRowId;

    public string Description
    {
        get => _model.Description;
        set
        {
            if (_model.Description != value)
            {
                _model.Description = value;
                OnPropertyChanged();
                _onChanged();
            }
        }
    }

    public bool IsReceived
    {
        get => _model.IsReceived;
        set
        {
            if (_model.IsReceived != value)
            {
                _model.IsReceived = value;
                _model.ReceivedDate = value ? DateTime.Now : null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReceivedDate));
                OnPropertyChanged(nameof(ReceivedDateDisplay));
                _onChanged();
            }
        }
    }

    public DateTime? ReceivedDate => _model.ReceivedDate;

    public string ReceivedDateDisplay =>
        _model.ReceivedDate.HasValue ? _model.ReceivedDate.Value.ToString("MM/dd/yyyy") : "";

    public string Notes
    {
        get => _model.Notes;
        set
        {
            if (_model.Notes != value)
            {
                _model.Notes = value;
                OnPropertyChanged();
                _onChanged();
            }
        }
    }

    public int SortOrder
    {
        get => _model.SortOrder;
        set
        {
            if (_model.SortOrder != value)
            {
                _model.SortOrder = value;
                OnPropertyChanged();
            }
        }
    }

    public ChecklistItem GetModel() => _model;
}
