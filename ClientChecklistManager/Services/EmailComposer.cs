using ClientChecklistManager.Models;

namespace ClientChecklistManager.Services;

public static class EmailComposer
{
    public static (string Subject, string HtmlBody) ComposeOutstandingItemsEmail(
        Client client, List<ChecklistItem> outstandingItems, int taxYear, AppSettings settings)
    {
        var subject = settings.EmailSubjectTemplate
            .Replace("{ClientName}", client.Name)
            .Replace("{ClientId}", client.ClientId)
            .Replace("{TaxYear}", taxYear.ToString());

        var itemRows = string.Join("\n",
            outstandingItems.Select((item, i) =>
                $"<tr><td style='padding:6px 12px;border-bottom:1px solid #eee;'>{i + 1}</td>" +
                $"<td style='padding:6px 12px;border-bottom:1px solid #eee;'>{System.Net.WebUtility.HtmlEncode(item.Description)}</td></tr>"));

        var closing = "Thank you,";
        if (!string.IsNullOrWhiteSpace(settings.FirmName))
        {
            closing = $"Thank you,<br/>{System.Net.WebUtility.HtmlEncode(settings.FirmName)}";
        }

        var htmlBody = $"""
            <html>
            <body style="font-family: Calibri, Arial, sans-serif; font-size: 14px; color: #333;">
            <p>Dear {System.Net.WebUtility.HtmlEncode(client.Name)},</p>

            <p>{System.Net.WebUtility.HtmlEncode(settings.EmailHeader)}</p>

            <p><strong>Client ID:</strong> {System.Net.WebUtility.HtmlEncode(client.ClientId)}</p>
            <p><strong>Tax Year:</strong> {taxYear}</p>

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

            <p>{System.Net.WebUtility.HtmlEncode(settings.EmailFooter)}</p>

            <p>{closing}</p>
            </body>
            </html>
            """;

        return (subject, htmlBody);
    }

    public static string ComposePreviewText(Client client, List<ChecklistItem> outstandingItems,
        int taxYear, AppSettings settings)
    {
        var subject = settings.EmailSubjectTemplate
            .Replace("{ClientName}", client.Name)
            .Replace("{ClientId}", client.ClientId)
            .Replace("{TaxYear}", taxYear.ToString());

        var closing = "Thank you,";
        if (!string.IsNullOrWhiteSpace(settings.FirmName))
        {
            closing = $"Thank you,\n{settings.FirmName}";
        }

        var lines = new List<string>
        {
            $"To: {client.Email}",
            $"Subject: {subject}",
            "",
            $"Dear {client.Name},",
            "",
            settings.EmailHeader,
            "",
            $"Client ID: {client.ClientId}",
            $"Tax Year: {taxYear}",
            "",
            "Outstanding items:",
            ""
        };

        for (int i = 0; i < outstandingItems.Count; i++)
        {
            lines.Add($"  {i + 1}. {outstandingItems[i].Description}");
        }

        lines.Add("");
        lines.Add(settings.EmailFooter);
        lines.Add("");
        lines.Add(closing);

        return string.Join(Environment.NewLine, lines);
    }
}
