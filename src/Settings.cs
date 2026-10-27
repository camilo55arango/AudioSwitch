using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AudioSwitch
{
    // Configuración en %AppData%\AudioSwitch\settings.ini (clave=valor), editable a mano.
    internal sealed class Settings
    {
        static readonly string[][] Defaults =
        {
            new[] { "HotkeyMicMute", "Ctrl+Alt+M" },
            new[] { "HotkeySpeakerMute", "Ctrl+Alt+A" },
            new[] { "HotkeyToggleProfile", "Ctrl+Alt+S" },
            new[] { "HotkeyProfile1", "Ctrl+Alt+1" },
            new[] { "HotkeyProfile2", "Ctrl+Alt+2" },
            new[] { "HotkeyWidget", "Ctrl+Alt+W" },
            new[] { "ShowWidget", "true" },
            new[] { "ShowOsd", "true" },
            new[] { "LinkMicToOutput", "true" },
            new[] { "WidgetX", "" },
            new[] { "WidgetY", "" },
            new[] { "Profile1Name", "" },
            new[] { "Profile1Output", "" },
            new[] { "Profile1Input", "" },
            new[] { "Profile2Name", "" },
            new[] { "Profile2Output", "" },
            new[] { "Profile2Input", "" },
        };

        const string Header =
            "# AudioSwitch - configuración\r\n" +
            "# Los atajos se configuran desde el menú de la bandeja > Configurar atajos.\r\n";

        readonly string path;
        readonly List<string> order = new List<string>();
        readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Settings()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioSwitch");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "settings.ini");
        }

        public string FilePath { get { return path; } }

        public bool FileExists { get { return File.Exists(path); } }

        public void Load()
        {
            order.Clear();
            values.Clear();
            foreach (string[] pair in Defaults) Set(pair[0], pair[1]);
            if (!File.Exists(path)) return;

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder(Header);
            foreach (string key in order) sb.Append(key).Append('=').Append(values[key]).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        public string Get(string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : "";
        }

        public static string GetDefault(string key)
        {
            foreach (string[] pair in Defaults)
                if (string.Equals(pair[0], key, StringComparison.OrdinalIgnoreCase)) return pair[1];
            return "";
        }

        public void Set(string key, string value)
        {
            if (!values.ContainsKey(key)) order.Add(key);
            values[key] = value ?? "";
        }

        public bool GetBool(string key)
        {
            return string.Equals(Get(key), "true", StringComparison.OrdinalIgnoreCase);
        }

        public void SetBool(string key, bool value)
        {
            Set(key, value ? "true" : "false");
        }

        public int? GetInt(string key)
        {
            int value;
            return int.TryParse(Get(key), out value) ? value : (int?)null;
        }
    }
}
