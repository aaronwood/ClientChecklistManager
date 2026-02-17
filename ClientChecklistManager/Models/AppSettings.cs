namespace ClientChecklistManager.Models;

public class AppSettings
{
    public const string DefaultBodyTemplate =
        "Dear {FirstName},\r\n\r\n" +
        "Below is a summary of the outstanding items we are still waiting to receive from you. " +
        "Please review and provide these at your earliest convenience.\r\n\r\n" +
        "Client ID: {ClientId}\r\n" +
        "Tax Year: {TaxYear}\r\n\r\n" +
        "{OutstandingItems}\r\n\r\n" +
        "If you have any questions, please don't hesitate to reach out.\r\n\r\n" +
        "Thank you,\r\n" +
        "{FirmName}";

    public const string DefaultSubjectTemplate =
        "Outstanding Items - {FirstName} ({ClientId}) - Tax Year {TaxYear}";

    public string OutlookFromAccount { get; set; } = string.Empty;
    public string EmailSubjectTemplate { get; set; } = DefaultSubjectTemplate;
    public string EmailBodyTemplate { get; set; } = DefaultBodyTemplate;
    public string EmailFontFamily { get; set; } = "Aptos";
    public int EmailFontSize { get; set; } = 11;
    public string DefaultBccAddress { get; set; } = string.Empty;
    public string FirmName { get; set; } = string.Empty;
    public int FollowUpReminderDays { get; set; } = 14;
}
