using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioSwitch
{
    // Atajos globales con RegisterHotKey; funcionan aunque la app no tenga el foco.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        const int WmHotkey = 0x0312;
        const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        readonly List<int> registered = new List<int>();

        public event Action<int> Pressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        // Un atajo vacío significa "desactivado" y no cuenta como error.
        public bool Register(int id, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return true;
            uint modifiers;
            Keys key;
            if (!TryParse(spec, out modifiers, out key)) return false;
            if (!RegisterHotKey(Handle, id, modifiers | ModNoRepeat, (uint)key)) return false;
            registered.Add(id);
            return true;
        }

        public void UnregisterAll()
        {
            foreach (int id in registered) UnregisterHotKey(Handle, id);
            registered.Clear();
        }

        public static bool TryParse(string spec, out uint modifiers, out Keys key)
        {
            modifiers = 0;
            key = Keys.None;
            foreach (string part in spec.Split('+'))
            {
                string token = part.Trim();
                switch (token.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control": modifiers |= ModControl; continue;
                    case "alt": modifiers |= ModAlt; continue;
                    case "shift": modifiers |= ModShift; continue;
                    case "win": modifiers |= ModWin; continue;
                }
                if (key != Keys.None || token.Length == 0) return false;
                if (token.Length == 1 && char.IsDigit(token[0])) token = "D" + token;
                Keys parsed;
                if (!Enum.TryParse(token, true, out parsed) || char.IsDigit(token[0])) return false;
                key = parsed;
            }
            return key != Keys.None;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }
}
