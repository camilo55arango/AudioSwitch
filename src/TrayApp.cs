using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AudioSwitch
{
    internal sealed class TrayApp : ApplicationContext
    {
        const int HkMic = 1, HkSpeaker = 2, HkToggleProfile = 3, HkProfile1 = 4, HkProfile2 = 5, HkWidget = 6;
        static readonly string[] HotkeySettings =
            { null, "HotkeyMicMute", "HotkeySpeakerMute", "HotkeyToggleProfile", "HotkeyProfile1", "HotkeyProfile2", "HotkeyWidget" };

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "AudioSwitch";
        const string NoDevice = "Ninguno";

        readonly AudioService audio = new AudioService();
        readonly Settings settings = new Settings();
        readonly HotkeyWindow hotkeys = new HotkeyWindow();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly TrayIconPainter iconPainter = new TrayIconPainter();
        readonly OsdForm osd = new OsdForm();
        readonly WidgetForm widget = new WidgetForm();
        readonly Timer poll = new Timer();

        AudioDevice output, input;
        bool? outputMuted, inputMuted;
        bool initialized;
        HotkeyDialog hotkeyDialog;

        public TrayApp()
        {
            settings.Load();
            if (!settings.FileExists) AutoDetectProfiles();
            settings.Save();

            menu.Opening += (s, e) => { BuildMenu(); e.Cancel = false; };
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleMic(); };

            widget.PopupMenu = menu;
            widget.SpeakerClicked += (s, e) => ToggleSpeaker();
            widget.MicClicked += (s, e) => ToggleMic();
            widget.Moved += (s, e) => SaveWidgetPosition();

            hotkeys.Pressed += OnHotkey;

            Refresh();
            initialized = true;
            tray.Visible = true;
            RegisterHotkeys();
            if (settings.GetBool("ShowWidget")) ShowWidget();

            // Sondeo ligero: detecta cambios hechos desde Windows, dispositivos que se conectan o desconectan, etc.
            poll.Interval = 500;
            poll.Tick += (s, e) => Refresh();
            poll.Start();
        }

        // ---------- Estado ----------

        void Refresh()
        {
            AudioDevice newOutput = audio.GetDefault(EDataFlow.Render);
            AudioDevice newInput = audio.GetDefault(EDataFlow.Capture);
            bool? newOutputMuted = newOutput == null ? null : audio.GetMute(newOutput.Id);
            bool? newInputMuted = newInput == null ? null : audio.GetMute(newInput.Id);

            bool outputChanged = IdOf(newOutput) != IdOf(output);
            bool inputChanged = IdOf(newInput) != IdOf(input);
            bool outputMuteChanged = !outputChanged && newOutputMuted != outputMuted;
            bool inputMuteChanged = !inputChanged && newInputMuted != inputMuted;

            output = newOutput;
            input = newInput;
            outputMuted = newOutputMuted;
            inputMuted = newInputMuted;

            if (!initialized || outputChanged || inputChanged || outputMuteChanged || inputMuteChanged)
                UpdateVisuals();
            if (initialized)
                Announce(outputChanged, inputChanged, outputMuteChanged, inputMuteChanged);
        }

        void Announce(bool outputChanged, bool inputChanged, bool outputMuteChanged, bool inputMuteChanged)
        {
            if (!settings.GetBool("ShowOsd")) return;

            if (outputChanged || inputChanged)
            {
                if (outputChanged && inputChanged)
                    osd.ShowMessage(Glyphs.Headphones, NameOf(output), "Micrófono: " + NameOf(input), Ui.Accent);
                else if (outputChanged)
                    osd.ShowMessage(Glyphs.Headphones, NameOf(output), "Salida de audio", output == null ? Ui.Disabled : Ui.Accent);
                else
                    osd.ShowMessage(Glyphs.Mic, NameOf(input), "Micrófono", input == null ? Ui.Disabled : Ui.Accent);
            }
            else if (inputMuteChanged && inputMuted != null)
            {
                if (inputMuted.Value) osd.ShowMessage(Glyphs.MicOff, "Micrófono silenciado", NameOf(input), Ui.Muted);
                else osd.ShowMessage(Glyphs.Mic, "Micrófono activo", NameOf(input), Ui.Live);
            }
            else if (outputMuteChanged && outputMuted != null)
            {
                if (outputMuted.Value) osd.ShowMessage(Glyphs.VolumeMute, "Audio silenciado", NameOf(output), Ui.Muted);
                else osd.ShowMessage(Glyphs.Volume, "Audio activo", NameOf(output), Ui.Live);
            }
        }

        void UpdateVisuals()
        {
            tray.Icon = iconPainter.Paint(inputMuted, outputMuted);
            string text = "Mic: " + StateText(inputMuted) + " · Audio: " + StateText(outputMuted);
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            widget.SetState(output == null ? null : output.Name, outputMuted, input == null ? null : input.Name, inputMuted);
        }

        static string StateText(bool? muted)
        {
            return muted == null ? "sin dispositivo" : (muted.Value ? "SILENCIADO" : "activo");
        }

        static string IdOf(AudioDevice device)
        {
            return device == null ? null : device.Id;
        }

        static string NameOf(AudioDevice device)
        {
            return device == null ? NoDevice : device.Name;
        }

        // ---------- Acciones ----------

        void ToggleMic()
        {
            if (input == null)
            {
                osd.ShowMessage(Glyphs.Warning, "No hay micrófono", "Conecta un dispositivo de entrada", Ui.Disabled);
                return;
            }
            audio.SetMute(input.Id, inputMuted != true);
            Refresh();
        }

        void ToggleSpeaker()
        {
            if (output == null)
            {
                osd.ShowMessage(Glyphs.Warning, "No hay salida de audio", "Conecta un dispositivo de salida", Ui.Disabled);
                return;
            }
            audio.SetMute(output.Id, outputMuted != true);
            Refresh();
        }

        void SelectOutput(AudioDevice device, bool linkMic)
        {
            audio.SetDefault(device.Id);
            if (linkMic)
            {
                AudioDevice mic = FindMatchingMic(device);
                if (mic != null) SelectInput(mic);
            }
            Refresh();
        }

        // El estado de silencio del micrófono anterior pasa al nuevo, para no quedar "al aire" por sorpresa.
        void SelectInput(AudioDevice device)
        {
            bool keepMuted = inputMuted == true;
            audio.SetDefault(device.Id);
            audio.SetMute(device.Id, keepMuted);
            Refresh();
        }

        AudioDevice FindMatchingMic(AudioDevice outputDevice)
        {
            if (string.IsNullOrEmpty(outputDevice.InterfaceName)) return null;
            return audio.GetDevices(EDataFlow.Capture)
                .Where(d => d.InterfaceName == outputDevice.InterfaceName)
                .OrderBy(d => IsMixDevice(d) ? 1 : 0)
                .FirstOrDefault();
        }

        static bool IsMixDevice(AudioDevice device)
        {
            string name = device.Name.ToLowerInvariant();
            return name.Contains("mezcla") || name.Contains("stereo mix") || name.Contains("estéreo");
        }

        // ---------- Perfiles ----------

        bool ProfileConfigured(int n)
        {
            return settings.Get("Profile" + n + "Output").Length > 0 || settings.Get("Profile" + n + "Input").Length > 0;
        }

        string ProfileName(int n)
        {
            string name = settings.Get("Profile" + n + "Name");
            return name.Length > 0 ? name : "Perfil " + n;
        }

        int CurrentProfile()
        {
            for (int n = 1; n <= 2; n++)
                if (ProfileConfigured(n) && settings.Get("Profile" + n + "Output") == (IdOf(output) ?? ""))
                    return n;
            return 0;
        }

        void ApplyProfile(int n)
        {
            if (!ProfileConfigured(n))
            {
                osd.ShowMessage(Glyphs.Warning, "Perfil " + n + " sin configurar", "Menú > Guardar configuración actual como…", Ui.Disabled);
                return;
            }

            AudioDevice profileOutput = audio.Find(settings.Get("Profile" + n + "Output"), EDataFlow.Render);
            AudioDevice profileInput = audio.Find(settings.Get("Profile" + n + "Input"), EDataFlow.Capture);
            if (profileOutput == null && profileInput == null)
            {
                osd.ShowMessage(Glyphs.Warning, ProfileName(n) + " no está conectado", "Revisa el cable o el puerto USB-C", Ui.Warn);
                return;
            }

            if (profileOutput != null) audio.SetDefault(profileOutput.Id);
            if (profileInput != null) SelectInput(profileInput);
            Refresh();
        }

        void ToggleProfile()
        {
            int next = CurrentProfile() == 1 ? 2 : 1;
            if (!ProfileConfigured(next) && ProfileConfigured(3 - next)) next = 3 - next;
            ApplyProfile(next);
        }

        void SaveProfile(int n)
        {
            settings.Set("Profile" + n + "Output", IdOf(output) ?? "");
            settings.Set("Profile" + n + "Input", IdOf(input) ?? "");
            string name = output != null && output.InterfaceName.Length > 0 ? output.InterfaceName : NameOf(output);
            settings.Set("Profile" + n + "Name", name);
            settings.Save();
            osd.ShowMessage(Glyphs.Headphones, "Guardado como perfil " + n, name, Ui.Accent);
        }

        // Primer arranque: crea un perfil por cada adaptador que tenga salida y micrófono (por ejemplo, el USB-C y el Realtek del jack).
        void AutoDetectProfiles()
        {
            List<AudioDevice> mics = audio.GetDevices(EDataFlow.Capture);
            HashSet<string> usedInterfaces = new HashSet<string>();
            int n = 1;
            foreach (AudioDevice speaker in audio.GetDevices(EDataFlow.Render).OrderBy(d => LooksLikeHeadset(d) ? 0 : 1))
            {
                if (n > 2) break;
                if (speaker.InterfaceName.Length == 0 || usedInterfaces.Contains(speaker.InterfaceName)) continue;
                AudioDevice mic = mics.Where(m => m.InterfaceName == speaker.InterfaceName)
                    .OrderBy(m => IsMixDevice(m) ? 1 : 0).FirstOrDefault();
                if (mic == null) continue;
                usedInterfaces.Add(speaker.InterfaceName);
                settings.Set("Profile" + n + "Name", speaker.InterfaceName);
                settings.Set("Profile" + n + "Output", speaker.Id);
                settings.Set("Profile" + n + "Input", mic.Id);
                n++;
            }
        }

        static bool LooksLikeHeadset(AudioDevice device)
        {
            string name = device.Name.ToLowerInvariant();
            return name.Contains("auricular") || name.Contains("headphone") || name.Contains("headset") || name.Contains("cascos");
        }

        // ---------- Atajos ----------

        void RegisterHotkeys()
        {
            hotkeys.UnregisterAll();
            List<string> failed = new List<string>();
            for (int id = HkMic; id <= HkWidget; id++)
            {
                string spec = settings.Get(HotkeySettings[id]);
                if (!hotkeys.Register(id, spec)) failed.Add(spec);
            }
            if (failed.Count > 0)
            {
                tray.ShowBalloonTip(6000, "AudioSwitch",
                    "No se pudieron registrar estos atajos (inválidos o en uso por otra app): " + string.Join(", ", failed) +
                    ". Cámbialos desde el menú > Configurar atajos.", ToolTipIcon.Warning);
            }
        }

        // Mientras la ventana está abierta los atajos se desactivan, para poder capturar las mismas combinaciones.
        void OpenHotkeyDialog()
        {
            if (hotkeyDialog != null)
            {
                hotkeyDialog.Activate();
                return;
            }

            hotkeys.UnregisterAll();
            List<KeyValuePair<string, string>> actions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("HotkeyMicMute", "Silenciar / activar micrófono"),
                new KeyValuePair<string, string>("HotkeySpeakerMute", "Silenciar / activar audio"),
                new KeyValuePair<string, string>("HotkeyToggleProfile", "Alternar perfil"),
                new KeyValuePair<string, string>("HotkeyProfile1", "Ir a " + ProfileName(1)),
                new KeyValuePair<string, string>("HotkeyProfile2", "Ir a " + ProfileName(2)),
                new KeyValuePair<string, string>("HotkeyWidget", "Mostrar / ocultar widget"),
            };
            hotkeyDialog = new HotkeyDialog(actions, settings.Get, TryApplyHotkeys);
            hotkeyDialog.FormClosed += (s, e) =>
            {
                if (hotkeyDialog.DialogResult != DialogResult.OK) RegisterHotkeys();
                hotkeyDialog = null;
            };
            hotkeyDialog.Show();
            hotkeyDialog.Activate();
        }

        // Registra los atajos nuevos; si alguno falla no guarda nada y devuelve cuáles fallaron.
        List<string> TryApplyHotkeys(Dictionary<string, string> values)
        {
            hotkeys.UnregisterAll();
            List<string> failed = new List<string>();
            for (int id = HkMic; id <= HkWidget; id++)
                if (!hotkeys.Register(id, values[HotkeySettings[id]])) failed.Add(HotkeySettings[id]);

            if (failed.Count > 0)
            {
                hotkeys.UnregisterAll();
                return failed;
            }

            foreach (KeyValuePair<string, string> pair in values) settings.Set(pair.Key, pair.Value);
            settings.Save();
            osd.ShowMessage(Glyphs.Headphones, "Atajos guardados", "Ya puedes usarlos", Ui.Accent);
            return failed;
        }

        void OnHotkey(int id)
        {
            switch (id)
            {
                case HkMic: ToggleMic(); break;
                case HkSpeaker: ToggleSpeaker(); break;
                case HkToggleProfile: ToggleProfile(); break;
                case HkProfile1: ApplyProfile(1); break;
                case HkProfile2: ApplyProfile(2); break;
                case HkWidget: SetWidgetVisible(!widget.Visible); break;
            }
        }

        // ---------- Widget ----------

        void ShowWidget()
        {
            widget.PlaceAt(settings.GetInt("WidgetX"), settings.GetInt("WidgetY"));
            widget.Show();
        }

        void SetWidgetVisible(bool visible)
        {
            if (visible) ShowWidget();
            else widget.Hide();
            settings.SetBool("ShowWidget", visible);
            settings.Save();
        }

        void SaveWidgetPosition()
        {
            settings.Set("WidgetX", widget.Left.ToString());
            settings.Set("WidgetY", widget.Top.ToString());
            settings.Save();
        }

        // ---------- Inicio con Windows ----------

        static string ExecutableCommand()
        {
            return "\"" + Application.ExecutablePath + "\"";
        }

        static bool StartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                object value = key == null ? null : key.GetValue(RunValueName);
                return value != null && string.Equals(value.ToString(), ExecutableCommand(), StringComparison.OrdinalIgnoreCase);
            }
        }

        static void SetStartup(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled) key.SetValue(RunValueName, ExecutableCommand());
                else key.DeleteValue(RunValueName, false);
            }
        }

        // ---------- Menú ----------

        void BuildMenu()
        {
            menu.Items.Clear();

            menu.Items.Add(Label("Salida: " + NameOf(output)));
            menu.Items.Add(Label("Micrófono: " + NameOf(input)));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Item("Silenciar micrófono", inputMuted == true, "HotkeyMicMute", ToggleMic));
            menu.Items.Add(Item("Silenciar audio", outputMuted == true, "HotkeySpeakerMute", ToggleSpeaker));
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem outputs = new ToolStripMenuItem("Dispositivo de salida");
            foreach (AudioDevice device in audio.GetDevices(EDataFlow.Render))
            {
                AudioDevice d = device;
                outputs.DropDownItems.Add(Item(d.Name, d.Id == IdOf(output), null,
                    () => SelectOutput(d, settings.GetBool("LinkMicToOutput"))));
            }
            if (outputs.DropDownItems.Count == 0) outputs.DropDownItems.Add(Label("(sin dispositivos)"));
            menu.Items.Add(outputs);

            ToolStripMenuItem inputs = new ToolStripMenuItem("Micrófono");
            foreach (AudioDevice device in audio.GetDevices(EDataFlow.Capture))
            {
                AudioDevice d = device;
                inputs.DropDownItems.Add(Item(d.Name, d.Id == IdOf(input), null, () => SelectInput(d)));
            }
            if (inputs.DropDownItems.Count == 0) inputs.DropDownItems.Add(Label("(sin dispositivos)"));
            menu.Items.Add(inputs);
            menu.Items.Add(new ToolStripSeparator());

            int current = CurrentProfile();
            for (int n = 1; n <= 2; n++)
            {
                int profile = n;
                ToolStripMenuItem item = Item(
                    ProfileConfigured(n) ? "Perfil " + n + ": " + ProfileName(n) : "Perfil " + n + " (sin configurar)",
                    current == n, "HotkeyProfile" + n, () => ApplyProfile(profile));
                item.Enabled = ProfileConfigured(n);
                menu.Items.Add(item);
            }
            ToolStripMenuItem alternate = Item("Alternar perfil", false, "HotkeyToggleProfile", ToggleProfile);
            alternate.Enabled = ProfileConfigured(1) || ProfileConfigured(2);
            menu.Items.Add(alternate);
            ToolStripMenuItem save = new ToolStripMenuItem("Guardar configuración actual como");
            save.DropDownItems.Add(Item("Perfil 1", false, null, () => SaveProfile(1)));
            save.DropDownItems.Add(Item("Perfil 2", false, null, () => SaveProfile(2)));
            menu.Items.Add(save);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Item("Mostrar widget", widget.Visible, "HotkeyWidget", () => SetWidgetVisible(!widget.Visible)));
            menu.Items.Add(Toggle("Mostrar avisos en pantalla", "ShowOsd"));
            menu.Items.Add(Toggle("Cambiar el micrófono junto con la salida", "LinkMicToOutput"));
            menu.Items.Add(Item("Iniciar con Windows", StartupEnabled(), null, () => SetStartup(!StartupEnabled())));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Item("Configurar atajos…", false, null, OpenHotkeyDialog));
            menu.Items.Add(Item("Salir", false, null, Exit));
        }

        static ToolStripMenuItem Label(string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Enabled = false;
            return item;
        }

        ToolStripMenuItem Item(string text, bool isChecked, string hotkeySetting, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = isChecked;
            if (hotkeySetting != null) item.ShortcutKeyDisplayString = settings.Get(hotkeySetting);
            item.Click += (s, e) => action();
            return item;
        }

        ToolStripMenuItem Toggle(string text, string setting)
        {
            return Item(text, settings.GetBool(setting), null, () =>
            {
                settings.SetBool(setting, !settings.GetBool(setting));
                settings.Save();
            });
        }

        void Exit()
        {
            if (hotkeyDialog != null) hotkeyDialog.Close();
            poll.Stop();
            hotkeys.Dispose();
            tray.Visible = false;
            tray.Dispose();
            iconPainter.Dispose();
            osd.Dispose();
            widget.Dispose();
            ExitThread();
        }
    }
}
