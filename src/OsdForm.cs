using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioSwitch
{
    // Aviso temporal en pantalla: no roba el foco y deja pasar los clics.
    internal sealed class OsdForm : Form
    {
        const int WsExTopmost = 0x8, WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
        const double FullOpacity = 0.95;

        readonly Timer hideTimer = new Timer();
        readonly Timer fadeTimer = new Timer();
        readonly Font glyphFont = Ui.GlyphFont(16f);
        readonly Font titleFont = new Font("Segoe UI Semibold", 11f);
        readonly Font subtitleFont = new Font("Segoe UI", 9f);

        string glyph = "";
        string title = "";
        string subtitle = "";
        Color accent = Ui.Accent;

        public OsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Ui.Background;
            DoubleBuffered = true;
            Size = new Size(Ui.S(340), Ui.S(72));
            Ui.RoundCorners(this, Ui.S(10));

            hideTimer.Interval = 1400;
            hideTimer.Tick += (s, e) => { hideTimer.Stop(); fadeTimer.Start(); };
            fadeTimer.Interval = 25;
            fadeTimer.Tick += (s, e) =>
            {
                if (Opacity <= 0.1) { fadeTimer.Stop(); Hide(); }
                else Opacity -= 0.1;
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate;
                return cp;
            }
        }

        public void ShowMessage(string glyph, string title, string subtitle, Color accent)
        {
            this.glyph = glyph;
            this.title = title;
            this.subtitle = subtitle ?? "";
            this.accent = accent;

            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - Ui.S(56));

            fadeTimer.Stop();
            hideTimer.Stop();
            Opacity = FullOpacity;
            if (!Visible) Show();
            Invalidate();
            hideTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Background);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int circle = Ui.S(44);
            Rectangle badge = new Rectangle(Ui.S(14), (Height - circle) / 2, circle, circle);
            using (SolidBrush brush = new SolidBrush(accent)) g.FillEllipse(brush, badge);
            TextRenderer.DrawText(g, glyph, glyphFont, badge, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int left = badge.Right + Ui.S(14);
            int width = Width - left - Ui.S(14);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(left, Ui.S(14), width, Ui.S(24)), Ui.Text, flags);
            TextRenderer.DrawText(g, subtitle, subtitleFont, new Rectangle(left, Ui.S(38), width, Ui.S(20)), Ui.SubText, flags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hideTimer.Dispose();
                fadeTimer.Dispose();
                glyphFont.Dispose();
                titleFont.Dispose();
                subtitleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
