using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class LayeredWindow : NativeWindow
{
    const int WS_EX_LAYERED = 0x80000;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int LWA_COLORKEY = 0x1;

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    public void CreateTransparentWindow()
    {
        IntPtr hwnd = CreateWindowEx(
            WS_EX_LAYERED, "STATIC", "",
            WS_POPUP, 100, 100, 200, 100,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        SetLayeredWindowAttributes(hwnd, 0x00FFFFFF, 0, LWA_COLORKEY);
        this.AssignHandle(hwnd);
    }
}