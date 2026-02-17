namespace ClientChecklistManager.Models;

public class Client
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? LastEmailed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string FullName => string.IsNullOrWhiteSpace(FirstName)
        ? LastName
        : string.IsNullOrWhiteSpace(LastName)
            ? FirstName
            : $"{FirstName} {LastName}";
}
