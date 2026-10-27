using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AudioSwitch
{
    // Mini panel flotante siempre visible: fila de salida y fila de micrófono.
    // Clic en una fila para silenciar o activar, arrastrar para mover, clic derecho para abrir el menú.
    internal sealed class WidgetForm : Form
    {
        const int WsExTopmost = 0x8, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
        const int RowSpeaker = 0, RowMic = 1;

        readonly Font glyphFont = Ui.GlyphFont(12f);
        readonly Font nameFont = new Font("Segoe UI", 9f);
        readonly Font statusFont = new Font("Segoe UI Semibold", 8.5f);
        readonly int rowHeight = Ui.S(32);
        readonly int padding = Ui.S(4);

        string speakerName, micName;
        bool? speakerMuted, micMuted;
        int hoverRow = -1;
        bool mouseDown, dragging;
        Point dragStartCursor, dragStartLocation;

        public event EventHandler SpeakerClicked;
        public event EventHandler MicClicked;
        public event EventHandler Moved;
        public ContextMenuStrip PopupMenu;

        public WidgetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Ui.Background;
            DoubleBuffered = true;
            Opacity = 0.94;
            Size = new Size(Ui.S(260), rowHeight * 2 + padding * 2);
            Ui.RoundCorners(this, Ui.S(8));
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
                return cp;
            }
        }

        public void SetState(string speakerName, bool? speakerMuted, string micName, bool? micMuted)
        {
            this.speakerName = speakerName;
            this.speakerMuted = speakerMuted;
            this.micName = micName;
            this.micMuted = micMuted;
            Invalidate();
        }

        public void PlaceAt(int? x, int? y)
        {
            Rectangle primary = Screen.PrimaryScreen.WorkingArea;
            Point location = x.HasValue && y.HasValue
                ? new Point(x.Value, y.Value)
                : new Point(primary.Right - Width - Ui.S(16), primary.Top + Ui.S(16));
            Rectangle bounds = new Rectangle(location, Size);
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))
                location = new Point(primary.Right - Width - Ui.S(16), primary.Top + Ui.S(16));
            Location = location;
        }

        Rectangle RowBounds(int row)
        {
            return new Rectangle(padding, padding + row * rowHeight, Width - padding * 2, rowHeight);
        }

        int RowAt(Point p)
        {
            for (int row = 0; row < 2; row++)
                if (RowBounds(row).Contains(p)) return row;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Background);
            DrawRow(g, RowSpeaker, speakerMuted == true ? Glyphs.VolumeMute : Glyphs.Volume, speakerName, speakerMuted);
            DrawRow(g, RowMic, micMuted == true ? Glyphs.MicOff : Glyphs.Mic, micName, micMuted);
        }

        void DrawRow(Graphics g, int row, string glyph, string name, bool? muted)
        {
            Rectangle r = RowBounds(row);
            Color accent = muted == null ? Ui.Disabled : (muted.Value ? Ui.Muted : Ui.Live);

            if (muted == true)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(45, Ui.Muted))) g.FillRectangle(brush, r);
            if (row == hoverRow)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(24, 255, 255, 255))) g.FillRectangle(brush, r);
            using (SolidBrush brush = new SolidBrush(accent))
                g.FillRectangle(brush, r.X, r.Y + Ui.S(7), Ui.S(3), r.Height - Ui.S(14));

            Rectangle glyphRect = new Rectangle(r.X + Ui.S(10), r.Y, Ui.S(22), r.Height);
            TextRenderer.DrawText(g, glyph, glyphFont, glyphRect, accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            string status = muted == null ? "—" : (muted.Value ? "OFF" : "ON");
            Size statusSize = TextRenderer.MeasureText(g, status, statusFont, Size.Empty, TextFormatFlags.NoPadding);
            Rectangle statusRect = new Rectangle(r.Right - Ui.S(10) - statusSize.Width, r.Y, statusSize.Width, r.Height);
            TextRenderer.DrawText(g, status, statusFont, statusRect, accent,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int nameLeft = glyphRect.Right + Ui.S(8);
            Rectangle nameRect = new Rectangle(nameLeft, r.Y, statusRect.Left - Ui.S(8) - nameLeft, r.Height);
            TextRenderer.DrawText(g, name ?? "Ninguno", nameFont, nameRect, muted == true ? Ui.SubText : Ui.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            mouseDown = true;
            dragging = false;
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (mouseDown)
            {
                Point now = Cursor.Position;
                int dx = now.X - dragStartCursor.X, dy = now.Y - dragStartCursor.Y;
                if (!dragging && Math.Abs(dx) + Math.Abs(dy) > Ui.S(4)) dragging = true;
                if (dragging) Location = new Point(dragStartLocation.X + dx, dragStartLocation.Y + dy);
                return;
            }
            int row = RowAt(e.Location);
            if (row != hoverRow)
            {
                hoverRow = row;
                Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                if (PopupMenu != null) PopupMenu.Show(Cursor.Position);
                return;
            }
            if (e.Button != MouseButtons.Left || !mouseDown) return;
            mouseDown = false;
            if (dragging)
            {
                dragging = false;
                if (Moved != null) Moved(this, EventArgs.Empty);
                return;
            }
            int row = RowAt(e.Location);
            if (row == RowSpeaker && SpeakerClicked != null) SpeakerClicked(this, EventArgs.Empty);
            if (row == RowMic && MicClicked != null) MicClicked(this, EventArgs.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverRow = -1;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                glyphFont.Dispose();
                nameFont.Dispose();
                statusFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
