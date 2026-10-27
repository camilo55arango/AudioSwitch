using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioSwitch
{
    // Campo que captura la combinación de teclas presionada y la muestra como "Ctrl+Alt+M".
    internal sealed class HotkeyBox : TextBox
    {
        const int VkLWin = 0x5B, VkRWin = 0x5C;
        static readonly Color FocusColor = Color.FromArgb(229, 241, 251);
        static readonly Color ErrorColor = Color.FromArgb(253, 226, 226);

        [DllImport("user32.dll")]
        static extern short GetKeyState(int vk);

        string hotkey = "";
        bool hasError;

        public HotkeyBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
            Cursor = Cursors.Hand;
            Width = Ui.S(200);
            UpdateLook();
        }

        public string Hotkey
        {
            get { return hotkey; }
            set
            {
                hotkey = value ?? "";
                hasError = false;
                ShowHotkey();
                UpdateLook();
            }
        }

        public bool HasError
        {
            get { return hasError; }
            set { hasError = value; UpdateLook(); }
        }

        void ShowHotkey()
        {
            Text = hotkey.Length == 0 ? "(sin atajo)" : hotkey;
        }

        void UpdateLook()
        {
            BackColor = hasError ? ErrorColor : (Focused ? FocusColor : SystemColors.Window);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            bool win = IsDown(VkLWin) || IsDown(VkRWin);

            if (modifiers == Keys.None && !win)
            {
                // Tab, Esc y Enter siguen sirviendo para navegar, cancelar y guardar.
                if (key == Keys.Tab || key == Keys.Escape || key == Keys.Enter)
                    return base.ProcessCmdKey(ref msg, keyData);
                if (key == Keys.Back || key == Keys.Delete)
                {
                    Hotkey = "";
                    return true;
                }
            }

            if (IsModifier(key))
            {
                Text = Describe(modifiers, win, Keys.None) + "…";
                return true;
            }
            if (modifiers == Keys.None && !win && !AllowedAlone(key))
            {
                Text = "Combina con Ctrl, Alt, Shift o Win";
                return true;
            }

            Hotkey = Describe(modifiers, win, key);
            return true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (Control.ModifierKeys == Keys.None) ShowHotkey();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            UpdateLook();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            ShowHotkey();
            UpdateLook();
        }

        static bool IsDown(int vk)
        {
            return (GetKeyState(vk) & 0x8000) != 0;
        }

        // Teclas que se pueden usar solas: no se necesitan para escribir. Las letras, números, Espacio, etc. siguen requiriendo modificador.
        static bool AllowedAlone(Keys key)
        {
            if (key >= Keys.F1 && key <= Keys.F24) return true;
            switch (key)
            {
                case Keys.Pause:
                case Keys.Scroll:
                case Keys.Insert:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return false;
        }

        // Nombres fijos para teclas con alias en el enum Keys (PageUp = Prior, PageDown = Next).
        static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            switch (key)
            {
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Scroll: return "Scroll";
            }
            return key.ToString();
        }

        static bool IsModifier(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        static string Describe(Keys modifiers, bool win, Keys key)
        {
            List<string> parts = new List<string>();
            if ((modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & Keys.Shift) != 0) parts.Add("Shift");
            if (win) parts.Add("Win");
            if (key != Keys.None) parts.Add(KeyName(key));
            return string.Join("+", parts);
        }
    }

    // Ventana para configurar los atajos. "apply" intenta registrarlos y devuelve las claves que fallaron.
    internal sealed class HotkeyDialog : Form
    {
        readonly Func<Dictionary<string, string>, List<string>> apply;
        readonly Dictionary<string, HotkeyBox> boxes = new Dictionary<string, HotkeyBox>();
        readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        readonly Label status = new Label();

        public HotkeyDialog(IList<KeyValuePair<string, string>> actions, Func<string, string> current,
            Func<Dictionary<string, string>, List<string>> apply)
        {
            this.apply = apply;

            Text = "Atajos de teclado - AudioSwitch";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Ui.S(12));

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 3;
            layout.AutoSize = true;
            layout.Dock = DockStyle.Fill;

            Label hint = new Label();
            hint.Text = "Haz clic en un campo y presiona la combinación que quieras.\r\n" +
                        "Inicio, Fin, Re Pág, Av Pág, Insert, Pausa y F1–F24 se pueden usar solas,\r\n" +
                        "pero dejarán de funcionar en los demás programas.\r\n" +
                        "Retroceso o Supr para dejarlo sin atajo.";
            hint.AutoSize = true;
            hint.Margin = new Padding(0, 0, 0, Ui.S(10));
            layout.Controls.Add(hint, 0, 0);
            layout.SetColumnSpan(hint, 3);

            int row = 1;
            foreach (KeyValuePair<string, string> action in actions)
            {
                Label label = new Label();
                label.Text = action.Value;
                label.AutoSize = true;
                label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, Ui.S(4), Ui.S(12), Ui.S(4));

                HotkeyBox box = new HotkeyBox();
                box.Hotkey = current(action.Key);
                box.Anchor = AnchorStyles.Left;

                Button clear = new Button();
                clear.Text = "Quitar";
                clear.AutoSize = true;
                clear.Anchor = AnchorStyles.Left;
                clear.Click += (s, e) => box.Hotkey = "";

                layout.Controls.Add(label, 0, row);
                layout.Controls.Add(box, 1, row);
                layout.Controls.Add(clear, 2, row);
                boxes[action.Key] = box;
                labels[action.Key] = action.Value;
                row++;
            }

            status.AutoSize = true;
            status.ForeColor = Color.FromArgb(185, 28, 28);
            status.MaximumSize = new Size(Ui.S(460), 0);
            status.Margin = new Padding(0, Ui.S(8), 0, 0);
            layout.Controls.Add(status, 0, row);
            layout.SetColumnSpan(status, 3);
            row++;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0, Ui.S(10), 0, 0);

            Button save = new Button();
            save.Text = "Guardar";
            save.AutoSize = true;
            save.Click += (s, e) => Save();

            Button cancel = new Button();
            cancel.Text = "Cancelar";
            cancel.AutoSize = true;
            cancel.DialogResult = DialogResult.Cancel;

            Button defaults = new Button();
            defaults.Text = "Restaurar predeterminados";
            defaults.AutoSize = true;
            defaults.Click += (s, e) =>
            {
                foreach (KeyValuePair<string, HotkeyBox> pair in boxes) pair.Value.Hotkey = Settings.GetDefault(pair.Key);
                status.Text = "";
            };

            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(defaults);
            layout.Controls.Add(buttons, 0, row);
            layout.SetColumnSpan(buttons, 3);

            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
        }

        void Save()
        {
            foreach (HotkeyBox box in boxes.Values) box.HasError = false;
            status.Text = "";

            List<string> duplicated = boxes
                .Where(p => p.Value.Hotkey.Length > 0)
                .GroupBy(p => p.Value.Hotkey, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(p => p.Key))
                .ToList();
            if (duplicated.Count > 0)
            {
                foreach (string key in duplicated) boxes[key].HasError = true;
                status.Text = "Hay atajos repetidos. Cada acción necesita una combinación distinta.";
                return;
            }

            Dictionary<string, string> values = boxes.ToDictionary(p => p.Key, p => p.Value.Hotkey);
            List<string> failed = apply(values);
            if (failed.Count > 0)
            {
                foreach (string key in failed) boxes[key].HasError = true;
                status.Text = "Estos atajos ya los usa Windows u otra aplicación: " +
                              string.Join(", ", failed.Select(k => boxes[k].Hotkey + " (" + labels[k] + ")")) +
                              ". Elige otra combinación.";
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
