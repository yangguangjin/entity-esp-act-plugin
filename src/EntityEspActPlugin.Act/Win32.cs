using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EntityEspActPlugin.Act;

internal static class Win32
{
    public const byte AcSrcOver = 0x00;
    public const byte AcSrcAlpha = 0x01;
    public const int UlwAlpha = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WinPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WinSize
    {
        public int Cx;
        public int Cy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref WinPoint pptDst,
        ref WinSize psize,
        IntPtr hdcSrc,
        ref WinPoint pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    public static bool TryGetFfxivClientRect(out Rectangle rect)
    {
        rect = Rectangle.Empty;
        foreach (var process in Process.GetProcessesByName("ffxiv_dx11"))
        {
            var handle = process.MainWindowHandle;
            if (TryGetClientRect(handle, out rect))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetFfxivClientRectText()
    {
        return TryGetFfxivClientRect(out var rect)
            ? $"FF14 client rect: x={rect.X}, y={rect.Y}, w={rect.Width}, h={rect.Height}"
            : "FF14 client rect: not found";
    }

    public static bool IsFfxivForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out var foregroundProcessId);
        if (foregroundProcessId == 0)
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName("ffxiv_dx11"))
        {
            if ((uint)process.Id == foregroundProcessId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetClientRect(IntPtr handle, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (handle == IntPtr.Zero || !IsWindowVisible(handle))
        {
            return false;
        }

        if (!GetClientRect(handle, out var client))
        {
            return false;
        }

        var origin = new POINT { X = client.Left, Y = client.Top };
        if (!ClientToScreen(handle, ref origin))
        {
            return false;
        }

        var width = Math.Max(1, client.Right - client.Left);
        var height = Math.Max(1, client.Bottom - client.Top);
        rect = new Rectangle(origin.X, origin.Y, width, height);
        return true;
    }
}
