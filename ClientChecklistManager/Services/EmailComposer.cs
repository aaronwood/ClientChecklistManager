using ClientChecklistManager.Models;

namespace ClientChecklistManager.Services;

public static class EmailComposer
{
    public static (string Subject, string HtmlBody) ComposeOutstandingItemsEmail(
        Client client, List<ChecklistItem> outstandingItems)
    {
        var subject = $"Outstanding Items - {client.Name} ({client.ClientId})";

        var itemRows = string.Join("\n",
            outstandingItems.Select((item, i) =>
                $"<tr><td style='padding:6px 12px;border-bottom:1px solid #eee;'>{i + 1}</td>" +
                $"<td style='padding:6px 12px;border-bottom:1px solid #eee;'>{System.Net.WebUtility.HtmlEncode(item.Description)}</td></tr>"));

        var htmlBody = $"""
            <html>
            <body style="font-family: Calibri, Arial, sans-serif; font-size: 14px; color: #333;">
            <p>Dear {System.Net.WebUtility.HtmlEncode(client.Name)},</p>

            <p>Below is a summary of the outstanding items we are still waiting to receive from you.
            Please review and provide these at your earliest convenience.</p>

            <p><strong>Client ID:</strong> {System.Net.WebUtility.HtmlEncode(client.ClientId)}</p>

            <table style="border-collapse:collapse; width:100%; max-width:600px; margin:12px 0;">
            <thead>
            <tr style="background-color:#f5f5f5;">
                <th style="padding:8px 12px; text-align:left; border-bottom:2px solid #ddd;">#</th>
                <th style="padding:8px 12px; text-align:left; border-bottom:2px solid #ddd;">Item</th>
            </tr>
            </thead>
            <tbody>
            {itemRows}
            </tbody>
            </table>

            <p>If you have any questions, please don't hesitate to reach out.</p>

            <p>Thank you,</p>
            </body>
            </html>
            """;

        return (subject, htmlBody);
    }

    public static string ComposePreviewText(Client client, List<ChecklistItem> outstandingItems)
    {
        var lines = new List<string>
        {
            $"To: {client.Email}",
            $"Subject: Outstanding Items - {client.Name} ({client.ClientId})",
            "",
            $"Dear {client.Name},",
            "",
            "Outstanding items we are waiting to receive:",
            ""
        };

        for (int i = 0; i < outstandingItems.Count; i++)
        {
            lines.Add($"  {i + 1}. {outstandingItems[i].Description}");
        }

        lines.Add("");
        lines.Add("If you have any questions, please don't hesitate to reach out.");
        lines.Add("");
        lines.Add("Thank you,");

        return string.Join(Environment.NewLine, lines);
    }
}
