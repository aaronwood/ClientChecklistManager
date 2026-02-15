namespace ClientChecklistManager.Models;

public class ChecklistTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
