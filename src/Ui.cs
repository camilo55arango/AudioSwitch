using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioSwitch
{
    internal static class Glyphs
    {
        public const string Mic = "";
        public const string MicOff = "";
        public const string Volume = "";
        public const string VolumeMute = "";
        public const string Headphones = "";
        public const string Warning = "";
    }

    internal static class Ui
    {
        public static readonly float Scale;
        public static readonly string GlyphFontName;

        public static readonly Color Background = Color.FromArgb(32, 32, 32);
        public static readonly Color Text = Color.FromArgb(240, 240, 240);
        public static readonly Color SubText = Color.FromArgb(160, 160, 160);
        public static readonly Color Live = Color.FromArgb(46, 160, 67);
        public static readonly Color Muted = Color.FromArgb(220, 38, 38);
        public static readonly Color Disabled = Color.FromArgb(110, 110, 110);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color Warn = Color.FromArgb(245, 158, 11);

        static Ui()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            using (InstalledFontCollection fonts = new InstalledFontCollection())
            {
                GlyphFontName = fonts.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
        }

        public static int S(int value)
        {
            return (int)Math.Round(value * Scale);
        }

        public static Font GlyphFont(float points)
        {
            return new Font(GlyphFontName, points, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static void RoundCorners(Form form, int radius)
        {
            Rectangle r = new Rectangle(0, 0, form.Width, form.Height);
            int d = radius * 2;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Region old = form.Region;
                form.Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }
    }

    // Icono de bandeja dibujado según el estado: verde con micrófono activo, rojo silenciado y punto naranja si el audio está silenciado.
    internal sealed class TrayIconPainter : IDisposable
    {
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        Icon current;
        IntPtr handle = IntPtr.Zero;

        public Icon Paint(bool? micMuted, bool? speakerMuted)
        {
            Size size = SystemInformation.SmallIconSize;
            IntPtr newHandle;
            using (Bitmap bmp = new Bitmap(size.Width, size.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Color fill = micMuted == null ? Ui.Disabled : (micMuted.Value ? Ui.Muted : Ui.Live);
                    using (SolidBrush brush = new SolidBrush(fill)) g.FillEllipse(brush, 0, 0, size.Width - 1, size.Height - 1);

                    string glyph = micMuted == true ? Glyphs.MicOff : Glyphs.Mic;
                    using (Font font = new Font(Ui.GlyphFontName, size.Height * 0.58f, GraphicsUnit.Pixel))
                    using (StringFormat format = new StringFormat())
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        g.DrawString(glyph, font, Brushes.White, new RectangleF(0, size.Height * 0.04f, size.Width, size.Height), format);
                    }

                    if (speakerMuted == true)
                    {
                        float badge = size.Width * 0.42f;
                        RectangleF r = new RectangleF(size.Width - badge, size.Height - badge, badge - 1, badge - 1);
                        using (SolidBrush brush = new SolidBrush(Ui.Warn)) g.FillEllipse(brush, r);
                        using (Pen pen = new Pen(Color.FromArgb(32, 32, 32), Math.Max(1f, size.Width / 16f))) g.DrawEllipse(pen, r);
                    }
                }
                newHandle = bmp.GetHicon();
            }

            Icon next = Icon.FromHandle(newHandle);
            Release();
            current = next;
            handle = newHandle;
            return current;
        }

        void Release()
        {
            if (current != null) current.Dispose();
            if (handle != IntPtr.Zero) DestroyIcon(handle);
            current = null;
            handle = IntPtr.Zero;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
