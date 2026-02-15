using System.Runtime.InteropServices;

namespace ClientChecklistManager.Services;

public class OutlookService
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        nint pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private static object GetActiveObject(string progId)
    {
        var clsid = Type.GetTypeFromProgID(progId)?.GUID
            ?? throw new COMException($"ProgID '{progId}' not found.");
        GetActiveObject(ref clsid, nint.Zero, out var obj);
        return obj;
    }

    private static dynamic GetOrCreateOutlookApp()
    {
        try
        {
            return GetActiveObject("Outlook.Application");
        }
        catch (COMException)
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType == null)
                throw new InvalidOperationException(
                    "Microsoft Outlook is not installed on this computer.");
            return Activator.CreateInstance(outlookType)!;
        }
    }

    /// <summary>
    /// Sends an email via the locally installed Outlook application using COM Interop.
    /// </summary>
    public static void SendEmail(string toAddress, string subject, string htmlBody,
        bool showPreview, string? fromAccount = null, string? bcc = null)
    {
        dynamic? outlookApp = null;
        try
        {
            outlookApp = GetOrCreateOutlookApp();

            dynamic mailItem = outlookApp!.CreateItem(0); // olMailItem = 0
            mailItem.To = toAddress;
            mailItem.Subject = subject;
            mailItem.HTMLBody = htmlBody;

            // Set send-from account if specified
            if (!string.IsNullOrEmpty(fromAccount))
            {
                try
                {
                    dynamic accounts = outlookApp.Session.Accounts;
                    for (int i = 1; i <= accounts.Count; i++)
                    {
                        dynamic account = accounts[i];
                        if (account.SmtpAddress == fromAccount || account.DisplayName == fromAccount)
                        {
                            mailItem.SendUsingAccount = account;
                            break;
                        }
                    }
                }
                catch
                {
                    // Silently fall back to default account if matching fails
                }
            }

            // Set BCC if specified
            if (!string.IsNullOrEmpty(bcc))
            {
                mailItem.BCC = bcc;
            }

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

    /// <summary>
    /// Returns a list of Outlook account email addresses/display names.
    /// </summary>
    public static List<string> GetAccountNames()
    {
        var accounts = new List<string>();
        dynamic? outlookApp = null;
        try
        {
            outlookApp = GetOrCreateOutlookApp();
            dynamic session = outlookApp!.Session;
            dynamic accts = session.Accounts;
            for (int i = 1; i <= accts.Count; i++)
            {
                dynamic account = accts[i];
                string smtpAddress = account.SmtpAddress;
                if (!string.IsNullOrEmpty(smtpAddress))
                    accounts.Add(smtpAddress);
            }
        }
        catch
        {
            // Outlook not available — return empty list
        }
        finally
        {
            if (outlookApp != null)
                Marshal.ReleaseComObject(outlookApp);
        }
        return accounts;
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
