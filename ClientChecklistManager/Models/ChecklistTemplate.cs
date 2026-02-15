namespace ClientChecklistManager.Models;

public class ChecklistTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
