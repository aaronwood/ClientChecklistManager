using System.Runtime.InteropServices;

namespace ClientChecklistManager.Services;

public class OutlookService
{
    /// <summary>
    /// Sends an email via the locally installed Outlook application using COM Interop.
    /// </summary>
    public static void SendEmail(string toAddress, string subject, string htmlBody, bool showPreview)
    {
        dynamic? outlookApp = null;
        try
        {
            // Try to get running Outlook instance first
            try
            {
                outlookApp = Marshal.GetActiveObject("Outlook.Application");
            }
            catch (COMException)
            {
                // Outlook not running — start it
                var outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                    throw new InvalidOperationException(
                        "Microsoft Outlook is not installed on this computer.");
                outlookApp = Activator.CreateInstance(outlookType);
            }

            dynamic mailItem = outlookApp!.CreateItem(0); // olMailItem = 0
            mailItem.To = toAddress;
            mailItem.Subject = subject;
            mailItem.HTMLBody = htmlBody;

            if (showPreview)
            {
                mailItem.Display(false);
            }
            else
            {
                mailItem.Send();
            }
        }
        finally
        {
            if (outlookApp != null)
                Marshal.ReleaseComObject(outlookApp);
        }
    }

    public static bool IsOutlookAvailable()
    {
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            return outlookType != null;
        }
        catch
        {
            return false;
        }
    }
}
