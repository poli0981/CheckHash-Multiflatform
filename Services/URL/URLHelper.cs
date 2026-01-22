using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CheckHash.Services;

public static class UrlHelper
{
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else // Linux
            {
                Process.Start("xdg-open", url);
            }
        }
        catch { /* Log error nếu cần */ }
    }
}