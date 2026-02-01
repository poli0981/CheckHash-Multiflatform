using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CheckHash.Services;

public static class UrlHelper
{
    public static void Open(string url)
    {
        if (!IsSafeUrl(url)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(url);
                Process.Start(startInfo);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Check for HTTP/HTTPS
        if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
            if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                return true;

        // Check for Directory
        // We only allow absolute paths to existing directories to prevent command injection via arguments
        try
        {
            // Ensure it's not a relative path that could be interpreted as a flag
            if (Path.IsPathRooted(url) && Directory.Exists(url)) return true;
        }
        catch
        {
            // Invalid path characters or other errors
            return false;
        }

        return false;
    }
}