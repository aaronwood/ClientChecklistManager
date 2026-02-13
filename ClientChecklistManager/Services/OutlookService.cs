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
                outlookApp = GetActiveObject("Outlook.Application");
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
