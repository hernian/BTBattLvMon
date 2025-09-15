using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTBattLvMon
{
    internal class LayeredWindow : Form
    {
        public LayeredWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            // this.TopMost = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                return cp;
            }
        }

        public void SetLayeredBitmap(Bitmap bitmap)
        {
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);

            Size size = bitmap.Size;
            Point pointSource = new Point(0, 0);
            Point topPos = new Point(this.Left, this.Top);

            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA
            };

            UpdateLayeredWindow(this.Handle, screenDC, ref topPos, ref size, memDC, ref pointSource, 0, ref blend, ULW_ALPHA);

            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCAPTION = 0x02;
            const int WM_NCLBUTTONDBLCLK = 0x00A3;

            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }
            if (m.Msg == WM_NCLBUTTONDBLCLK)
            {
                // キャプション領域のダブルクリックでウィンドウを閉じる
                this.Close();
                return;
            }
            base.WndProc(ref m);
        }

        // Win32 API定義
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }
    }
}
