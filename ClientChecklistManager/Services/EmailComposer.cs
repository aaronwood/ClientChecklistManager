using System.Text;
using ClientChecklistManager.Models;

namespace ClientChecklistManager.Services;

public static class EmailComposer
{
    public static (string Subject, string HtmlBody) ComposeOutstandingItemsEmail(
        Client client, List<ChecklistItem> outstandingItems, int taxYear, AppSettings settings)
    {
        var subject = ReplaceTags(settings.EmailSubjectTemplate, client, taxYear, settings);

        // HTML-encode the body template so user text is safe
        var htmlBody = System.Net.WebUtility.HtmlEncode(settings.EmailBodyTemplate);

        // Replace tags with HTML-encoded values
        htmlBody = htmlBody
            .Replace("{FirstName}", System.Net.WebUtility.HtmlEncode(client.FirstName))
            .Replace("{LastName}", System.Net.WebUtility.HtmlEncode(client.LastName))
            .Replace("{ClientId}", System.Net.WebUtility.HtmlEncode(client.ClientId))
            .Replace("{ClientEmail}", System.Net.WebUtility.HtmlEncode(client.Email))
            .Replace("{TaxYear}", taxYear.ToString())
            .Replace("{FirmName}", System.Net.WebUtility.HtmlEncode(settings.FirmName));

        // Convert line breaks to HTML
        htmlBody = htmlBody.Replace("\r\n", "<br/>\n").Replace("\n", "<br/>\n");

        // Generate outstanding items table and replace tag
        var itemsHtml = GenerateItemsTable(outstandingItems);
        htmlBody = htmlBody.Replace("{OutstandingItems}", itemsHtml);

        // Wrap in HTML with configured font
        var fontFamily = string.IsNullOrWhiteSpace(settings.EmailFontFamily)
            ? "Aptos, Calibri, Arial, sans-serif"
            : $"{settings.EmailFontFamily}, Calibri, Arial, sans-serif";
        var fontSize = settings.EmailFontSize > 0 ? settings.EmailFontSize : 11;

        htmlBody = $"""
            <html>
            <body style="font-family: {fontFamily}; font-size: {fontSize}pt; color: #333;">
            {htmlBody}
            </body>
            </html>
            """;

        return (subject, htmlBody);
    }

    public static string ComposePreviewText(Client client, List<ChecklistItem> outstandingItems,
        int taxYear, AppSettings settings)
    {
        var subject = ReplaceTags(settings.EmailSubjectTemplate, client, taxYear, settings);

        var body = ReplaceTags(settings.EmailBodyTemplate, client, taxYear, settings);

        // Generate plain text items list
        var itemsList = new StringBuilder();
        for (int i = 0; i < outstandingItems.Count; i++)
        {
            itemsList.AppendLine($"  {i + 1}. {outstandingItems[i].Description}");
        }

        body = body.Replace("{OutstandingItems}", itemsList.ToString().TrimEnd());

        return $"To: {client.Email}\nSubject: {subject}\n\n{body}";
    }

    private static string ReplaceTags(string template, Client client, int taxYear, AppSettings settings)
    {
        return template
            .Replace("{FirstName}", client.FirstName)
            .Replace("{LastName}", client.LastName)
            .Replace("{ClientName}", client.FullName) // backward compatibility
            .Replace("{ClientId}", client.ClientId)
            .Replace("{ClientEmail}", client.Email)
            .Replace("{TaxYear}", taxYear.ToString())
            .Replace("{FirmName}", settings.FirmName);
    }

    private static string GenerateItemsTable(List<ChecklistItem> outstandingItems)
    {
        var itemRows = string.Join("\n",
            outstandingItems.Select((item, i) =>
                $"<tr><td style='padding:6px 12px;border-bottom:1px solid #eee;'>{i + 1}</td>" +
                $"<td style='padding:6px 12px;border-bottom:1px solid #eee;'>{System.Net.WebUtility.HtmlEncode(item.Description)}</td></tr>"));

        return $"""
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
            """;
    }
}
