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

        // Convert line breaks to HTML: double newlines become paragraph breaks, single newlines become <br/>
        htmlBody = htmlBody
            .Replace("\r\n", "\n")               // Normalize to \n
            .Replace("\n\n", "</p><p>")           // Double newline = paragraph break
            .Replace("\n", "<br/>\n");            // Single newline = line break
        htmlBody = "<p>" + htmlBody + "</p>";     // Wrap in paragraph tags

        // Resolve font settings for use in body and table
        var fontFamily = string.IsNullOrWhiteSpace(settings.EmailFontFamily)
            ? "Aptos, Calibri, Arial, sans-serif"
            : $"{settings.EmailFontFamily}, Calibri, Arial, sans-serif";
        var fontSize = settings.EmailFontSize > 0 ? settings.EmailFontSize : 11;

        // Generate outstanding items table and replace tag
        var itemsHtml = GenerateItemsTable(outstandingItems, fontFamily, fontSize);
        htmlBody = htmlBody.Replace("{OutstandingItems}", itemsHtml);

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

    private static string GenerateItemsTable(List<ChecklistItem> outstandingItems, string fontFamily, int fontSize)
    {
        // Scale padding proportionally to font size (baseline: 11pt -> 2px/4px vertical, 6px horizontal)
        var vPad = Math.Max(1, (int)Math.Round(fontSize * 2.0 / 11));
        var hPad = Math.Max(2, (int)Math.Round(fontSize * 6.0 / 11));
        var cellPad = $"{vPad}px {hPad}px";
        var headerPad = $"{vPad + 1}px {hPad}px";
        var fontStyle = $"font-family:{fontFamily};font-size:{fontSize}pt;";
        var itemRows = string.Join("\n",
            outstandingItems.Select((item, i) =>
                $"<tr><td style='padding:{cellPad};border-bottom:1px solid #eee;{fontStyle}'>{i + 1}</td>" +
                $"<td style='padding:{cellPad};border-bottom:1px solid #eee;{fontStyle}'>{System.Net.WebUtility.HtmlEncode(item.Description)}</td></tr>"));

        return $"""
            <table style="border-collapse:collapse; width:100%; max-width:600px; margin:8px 0;">
            <thead>
            <tr style="background-color:#f5f5f5;">
                <th style="padding:{headerPad}; text-align:left; border-bottom:2px solid #ddd;{fontStyle}">#</th>
                <th style="padding:{headerPad}; text-align:left; border-bottom:2px solid #ddd;{fontStyle}">Item</th>
            </tr>
            </thead>
            <tbody>
            {itemRows}
            </tbody>
            </table>
            """;
    }
}
