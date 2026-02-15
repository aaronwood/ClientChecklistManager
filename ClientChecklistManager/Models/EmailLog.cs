namespace ClientChecklistManager.Models;

public class EmailLog
{
    public int Id { get; set; }
    public int ClientRowId { get; set; }
    public DateTime SentAt { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int OutstandingItemCount { get; set; }
    public int TaxYear { get; set; } = DateTime.Now.Year;
}
