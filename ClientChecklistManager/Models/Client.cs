namespace ClientChecklistManager.Models;

public class Client
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? LastEmailed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
