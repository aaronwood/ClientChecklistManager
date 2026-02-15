namespace ClientChecklistManager.Models;

public class AppSettings
{
    public string OutlookFromAccount { get; set; } = string.Empty;
    public string EmailSubjectTemplate { get; set; } = "Outstanding Items - {ClientName} ({ClientId}) - Tax Year {TaxYear}";
    public string EmailHeader { get; set; } = "Below is a summary of the outstanding items we are still waiting to receive from you. Please review and provide these at your earliest convenience.";
    public string EmailFooter { get; set; } = "If you have any questions, please don't hesitate to reach out.";
    public string DefaultBccAddress { get; set; } = string.Empty;
    public string FirmName { get; set; } = string.Empty;
    public int FollowUpReminderDays { get; set; } = 14;
}
