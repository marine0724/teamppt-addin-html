using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TeampptAddin
{
    /// <summary>
    /// 드래그 중 커서를 따라다니는 반투명 고스트 윈도우.
    /// Win32 Layered Window API로 per-pixel alpha 렌더링.
    ///
    /// 윈도우 스타일:
    /// - WS_EX_LAYERED: UpdateLayeredWindow로 알파 블렌딩
    /// - WS_EX_TRANSPARENT: 마우스 이벤트 통과 (클릭 가로채지 않음)
    /// - WS_EX_NOACTIVATE: 포커스 빼앗지 않음
    /// - WS_EX_TOOLWINDOW: 작업 표시줄에 안 나타남
    /// - WS_EX_TOPMOST: 항상 최상위
    ///
    /// 렌더링:
    /// - 썸네일 이미지를 32bpp ARGB 비트맵으로 변환 (BuildAlphaBitmap)
    /// - 화면 너비의 85%까지 스케일 (너무 큰 이미지 방지)
    /// - SourceConstantAlpha = 180 (~70% 불투명도) + 이미지 자체 알파
    /// - MoveTo()로 커서 중앙에 위치 (offset = -Width/2, -Height/2)
    /// </summary>
    internal class GhostWindow : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObj);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObj);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        const int ULW_ALPHA = 2;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOPMOST = 0x00000008;

        private Bitmap _alphaBmp;

        public GhostWindow(Image thumb)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            _alphaBmp = BuildAlphaBitmap(thumb);
            Size = _alphaBmp.Size;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT
                            | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyAlpha();
        }

        public void MoveTo(Point screenPos)
        {
            Location = new Point(
                screenPos.X - Width / 2,
                screenPos.Y - Height / 2);
        }

        private void ApplyAlpha()
        {
            if (_alphaBmp == null || !IsHandleCreated) return;

            var screenDC = GetDC(IntPtr.Zero);
            var memDC = CreateCompatibleDC(screenDC);
            var hBmp = _alphaBmp.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
            var prev = SelectObject(memDC, hBmp);

            var ptDst = new POINT { X = Left, Y = Top };
            var ptSrc = new POINT { X = 0, Y = 0 };
            var sz = new SIZE { cx = _alphaBmp.Width, cy = _alphaBmp.Height };
            var blend = new BLENDFUNCTION
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 180,
                AlphaFormat = 1
            };

            UpdateLayeredWindow(Handle, screenDC, ref ptDst, ref sz,
                memDC, ref ptSrc, 0, ref blend, ULW_ALPHA);

            SelectObject(memDC, prev);
            DeleteObject(hBmp);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }

        private static Bitmap BuildAlphaBitmap(Image src)
        {
            if (src == null)
            {
                var fb = new Bitmap(200, 30, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(fb))
                    g.Clear(Color.FromArgb(200, 39, 39, 42));
                return fb;
            }

            // Shape.Export는 72~96 DPI로 뽑아서 PPT 화면보다 작음.
            // 슬라이드 화면 기준 ~75% 느낌을 내려면 export 크기를 약간 키워야 함.
            var screenW = Screen.PrimaryScreen.WorkingArea.Width;
            const int stdSlideExportW = 1280; // 16:9 슬라이드 Shape.Export 기준 너비 (96DPI)
            float targetFullSlideW = screenW * 0.7f; // PPT 편집 뷰에서 슬라이드가 차지하는 대략적 너비
            float upscale = (targetFullSlideW / stdSlideExportW) * 0.75f;

            int w = Math.Max(40, (int)(src.Width * upscale));
            int h = Math.Max(40, (int)(src.Height * upscale));

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.DrawImage(src, 0, 0, w, h);
            }
            return bmp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _alphaBmp?.Dispose(); _alphaBmp = null; }
            base.Dispose(disposing);
        }
    }
}
