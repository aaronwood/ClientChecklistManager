namespace ClientChecklistManager.Models;

public class ChecklistItem
{
    public int Id { get; set; }
    public int ClientRowId { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsReceived { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int TaxYear { get; set; } = DateTime.Now.Year;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
