using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Jwmv.Gui.Services;

internal static class ClipboardHelper
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static bool TrySetText(string text, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrEmpty(text))
        {
            errorMessage = "Clipboard text is empty.";
            return false;
        }

        // Prefer CRLF on Windows so multiline PowerShell scripts survive paste.
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        Exception? lastError = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                if (TrySetTextNative(normalized))
                {
                    return true;
                }

                Clipboard.SetDataObject(normalized, copy: true);
                return true;
            }
            catch (Exception exception) when (exception is ExternalException or COMException or InvalidOperationException)
            {
                lastError = exception;
                Thread.Sleep(50 * (attempt + 1));
            }
        }

        errorMessage = lastError?.Message ?? "OpenClipboard failed.";
        return false;
    }

    public static string SaveTextFile(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static bool TrySetTextNative(string text)
    {
        var hwnd = IntPtr.Zero;
        if (Application.Current?.MainWindow is not null)
        {
            hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
        }

        if (!OpenClipboard(hwnd))
        {
            return false;
        }

        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            var hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (hGlobal == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(hGlobal);
            if (pointer == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            // Ownership transferred to the system on success.
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
