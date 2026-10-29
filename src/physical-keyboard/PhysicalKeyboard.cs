using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PsKeyboard.PhysicalRelay;

namespace PSKeyboard
{
    internal sealed class PhysicalKeyboard : Form
    {
        private const int ListenPort = 33251, UpstreamPort = 3240, ToggleHotkey = 0x504C;
        private readonly KeyboardControlPipeline input;
        private readonly Stopwatch inputClock = Stopwatch.StartNew();
        private volatile BindingProfile profile;
        private readonly string profilePath;
        private readonly bool preview;
        private readonly TextBox bus = new TextBox();
        private readonly NumericUpDown endpoint = new NumericUpDown();
        private readonly ComboBox product = new ComboBox();
        private readonly Button start = new Button(), toggle = new Button(), stop = new Button();
        private readonly Label status = new Label(), keyboardStatus = new Label(), notice = new Label();
        private readonly StatusDot controllerDot = new StatusDot(), keyboardDot = new StatusDot();
        private readonly Button configure = new Button(), advancedButton = new Button();
        private readonly Panel advanced = new Panel();
        private readonly Label[] bindingLabels = new Label[22];
        private readonly System.Windows.Forms.Timer timer;
        private BindingEditor activeEditor;
        private readonly string sonyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Sony", "PS Remote Play", "RemotePlay.exe");
        private readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;
        private readonly OptionalStatusFile statusFile;
        private volatile UsbIpRelay relay;
        private volatile string startupFailure;
        private volatile bool enabled, closing, editing;
        private IntPtr hook;
        private HookCallback hookCallback;
        private bool hotkeyRegistered;
        private uint lastFocusPid;
        private long lastFocusCheck, lastReportAt, lastReportCount;
        private bool hadAttachment;
        private sealed class FocusDecision
        {
            public readonly uint Pid;
            public readonly bool Allowed;
            public FocusDecision(uint pid, bool allowed) { Pid = pid; Allowed = allowed; }
        }
        private volatile FocusDecision hookFocus = new FocusDecision(0, false);

        private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)]
        private struct KeyData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, HookCallback callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);

        [STAThread]
        private static int Main(string[] args)
        {
            // Preview is a separate path: only our own transparent controls,
            // never a hook, mutex, timer, profile/status file or relay.
            if (args.Length > 0 && (args[0] == "--render-preview" || args[0] == "--render-editor-preview"))
                return RenderPreview(args);
            PhysicalKeyboardOptions options;
            try { options = PhysicalKeyboardOptions.Parse(args); }
            catch (ArgumentException)
            {
                MessageBox.Show(UiText.T("InvalidArguments"), UiText.T("AppTitle"));
                return 2;
            }
            bool created;
            using (Mutex instance = new Mutex(true, "Local\\PSKeyboardPhysicalRelay", out created))
            {
                if (!created) { MessageBox.Show(UiText.T("AlreadyRunning"), UiText.T("AppTitle")); return 1; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new PhysicalKeyboard(options.BusId, options.StartRelay, options.StatusFile, false));
            }
            return 0;
        }

        private static int RenderPreview(string[] args)
        {
            try
            {
                if (args.Length != 2 && args.Length != 4) return 2;
                string path = args[1];
                if (String.IsNullOrWhiteSpace(path) || path.Length < 4 ||
                    !Char.IsLetter(path[0]) || path[1] != ':' || (path[2] != '\\' && path[2] != '/') ||
                    path.IndexOf(':', 2) >= 0 || !String.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) return 2;
                path = Path.GetFullPath(path);
                if (!Directory.Exists(Path.GetDirectoryName(path))) return 2;
                if (args.Length == 4)
                {
                    if (args[2] != "--preview-language") return 2;
                    UiText.Initialize(CultureInfo.GetCultureInfo(args[3]));
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (Form form = args[0] == "--render-editor-preview" ? (Form)new BindingEditor(BindingProfile.Default(), true) :
                    new PhysicalKeyboard("1-2", false, null, true))
                {
                    form.CreateControl(); CreatePreviewControls(form); form.PerformLayout();
                    // DataGridView skips painting while its parent is invisible.
                    // A transparent, off-screen, non-activating preview window
                    // supplies that layout state without exposing a desktop UI.
                    form.Opacity = 0; form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
                    form.Show(); form.Update(); Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
                    form.Hide();
                }
                return 0;
            }
            catch { return 2; }
        }

        protected override bool ShowWithoutActivation { get { return preview; } }

        private static void CreatePreviewControls(Control control)
        {
            // Accessing Handle forces creation for an unshown ancestor. Plain
            // CreateControl skips invisible descendants and renders a blank form.
            IntPtr handle = control.Handle;
            foreach (Control child in control.Controls) CreatePreviewControls(child);
            control.PerformLayout();
        }

        private PhysicalKeyboard(string busId, bool autoStartRelay, string statusPath, bool renderPreview)
        {
            preview = renderPreview;
            profile = BindingProfile.Default();
            profilePath = preview ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSKeyboard", "bindings.json");
            bool profileWarning = false;
            if (!preview)
            {
                try { profile = BindingProfile.Load(profilePath); }
                catch { profileWarning = true; }
                statusFile = new OptionalStatusFile(statusPath, GetStatusSnapshot);
            }
            input = new KeyboardControlPipeline(delegate { return profile; });
            Text = UiText.T("AppTitle"); ClientSize = new Size(900, 720); MinimumSize = new Size(850, 710);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Font = KeyboardUi.Font(10, FontStyle.Regular); ForeColor = KeyboardUi.Ink; BackColor = KeyboardUi.Background;
            BuildInterface(busId);
            if (profileWarning) { notice.Text = UiText.T("ProfileLoadWarning"); notice.ForeColor = KeyboardUi.Amber; }
            if (preview) return;
            timer = new System.Windows.Forms.Timer();
            Shown += delegate {
                InstallKeyboardHook();
                statusFile.Start();
                // Only an explicit CLI option starts the loopback proxy. This
                // does not bind/import hardware or enable keyboard capture.
                if (autoStartRelay && hotkeyRegistered)
                    BeginInvoke(new Action(StartRelay));
            };
            timer.Interval = 100; timer.Tick += delegate { RefreshState(); }; timer.Start();
        }

        private void BuildInterface(string busId)
        {
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 22, 28, 18) };
            Controls.Add(scroll);
            TableLayoutPanel root = KeyboardUi.Table(1); root.Dock = DockStyle.Top; root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink; root.RowCount = 7;
            int[] heights = { 88, 108, 68, 318, 44, 0, 42 };
            for (int i = 0; i < heights.Length; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, heights[i]));
            scroll.Controls.Add(root);

            TableLayoutPanel header = KeyboardUi.Table(1); header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 47)); header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label brand = KeyboardUi.Label(UiText.T("AppTitle"), 27, KeyboardUi.Ink);
            brand.Font = KeyboardUi.Font(27, FontStyle.Bold); brand.Dock = DockStyle.Fill;
            Label subtitle = KeyboardUi.Label(UiText.T("AppSubtitle"), 10, KeyboardUi.Muted);
            subtitle.Dock = DockStyle.Fill;
            header.Controls.Add(brand, 0, 0); header.Controls.Add(subtitle, 0, 1); root.Controls.Add(header, 0, 0);

            TableLayoutPanel cards = KeyboardUi.Table(2);
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cards.Controls.Add(StatusCard(UiText.T("ControllerTitle"), status, controllerDot, true), 0, 0);
            cards.Controls.Add(StatusCard(UiText.T("KeyboardTitle"), keyboardStatus, keyboardDot, false), 1, 0);
            root.Controls.Add(cards, 0, 1);
            SetController(KeyboardUi.Gray, "ControllerDisconnected"); SetKeyboard(KeyboardUi.Gray, "KeyboardPaused");

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 14, 0, 0), Margin = Padding.Empty, WrapContents = false };
            KeyboardUi.Button(toggle, UiText.T("EnableKeyboard"), true); toggle.Enabled = false;
            KeyboardUi.Button(configure, UiText.T("ConfigureKeys"), false);
            toggle.Click += delegate { Toggle(); }; configure.Click += delegate { EditBindings(); };
            actions.Controls.AddRange(new Control[] { toggle, configure }); root.Controls.Add(actions, 0, 2);

            TableLayoutPanel bindings = KeyboardUi.Table(2); bindings.Padding = new Padding(18, 12, 18, 12); bindings.BackColor = Color.White;
            bindings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); bindings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bindings.RowCount = 11;
            for (int row = 0; row < 11; row++) bindings.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 11));
            for (int i = 0; i < BindingProfile.ActionCount; i++)
            {
                TableLayoutPanel item = KeyboardUi.Table(2); item.Padding = new Padding(8, 0, 12, 0);
                item.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); item.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
                Label action = KeyboardUi.Label(UiText.ActionName(i), 9, KeyboardUi.Ink); action.Dock = DockStyle.Fill;
                Label key = KeyboardUi.Label(UiText.KeyName(profile.KeyForAction(i)), 9, KeyboardUi.Accent); key.Font = KeyboardUi.Font(9, FontStyle.Bold);
                key.TextAlign = ContentAlignment.MiddleRight; key.Dock = DockStyle.Fill; bindingLabels[i] = key;
                item.Controls.Add(action, 0, 0); item.Controls.Add(key, 1, 0); bindings.Controls.Add(item, i / 11, i % 11);
            }
            root.Controls.Add(bindings, 0, 3);
            Label guide = KeyboardUi.Label(UiText.T("ReadyHint"), 9, KeyboardUi.Muted); guide.Dock = DockStyle.Fill; root.Controls.Add(guide, 0, 4);

            BuildAdvanced(busId); advanced.Visible = false; root.Controls.Add(advanced, 0, 5);
            TableLayoutPanel footer = KeyboardUi.Table(2);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            notice.Dock = DockStyle.Fill; notice.ForeColor = KeyboardUi.Muted; notice.TextAlign = ContentAlignment.MiddleLeft;
            notice.Font = KeyboardUi.Font(9, FontStyle.Regular); notice.Text = UiText.T("LanguageLabel") + " · " + UiText.LanguageTag;
            advancedButton.Text = UiText.T("AdvancedShow"); advancedButton.FlatStyle = FlatStyle.Flat;
            advancedButton.FlatAppearance.BorderSize = 0; advancedButton.AutoSize = true; advancedButton.ForeColor = KeyboardUi.Muted;
            advancedButton.Padding = new Padding(8); advancedButton.Cursor = Cursors.Hand;
            advancedButton.Click += delegate {
                bool show = !advanced.Visible; advanced.Visible = show;
                root.RowStyles[5].Height = show ? 198 : 0;
                advancedButton.Text = UiText.T(show ? "AdvancedHide" : "AdvancedShow");
            };
            footer.Controls.Add(notice, 0, 0); footer.Controls.Add(advancedButton, 1, 0); root.Controls.Add(footer, 0, 6);
        }

        private Panel StatusCard(string title, Label value, StatusDot dot, bool left)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White,
                Margin = left ? new Padding(0, 0, 8, 0) : new Padding(8, 0, 0, 0) };
            TableLayoutPanel content = KeyboardUi.Table(1); content.Padding = new Padding(20, 12, 20, 12); content.RowCount = 2;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label caption = KeyboardUi.Label(title, 9, KeyboardUi.Muted); caption.Dock = DockStyle.Fill;
            TableLayoutPanel state = KeyboardUi.Table(2);
            state.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28)); state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dot.Anchor = AnchorStyles.Left; dot.Margin = Padding.Empty;
            value.Dock = DockStyle.Fill; value.Margin = Padding.Empty;
            value.Font = KeyboardUi.Font(11, FontStyle.Bold); value.ForeColor = KeyboardUi.Ink;
            value.TextAlign = ContentAlignment.MiddleLeft;
            state.Controls.Add(dot, 0, 0); state.Controls.Add(value, 1, 0);
            content.Controls.Add(caption, 0, 0); content.Controls.Add(state, 0, 1); card.Controls.Add(content); return card;
        }

        private void BuildAdvanced(string busId)
        {
            advanced.Dock = DockStyle.Fill; advanced.Margin = new Padding(0, 0, 0, 8); advanced.BackColor = Color.White;
            TableLayoutPanel grid = KeyboardUi.Table(3); grid.Padding = new Padding(18, 10, 18, 10); grid.RowCount = 4;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            string[] labels = { "BusId", "Product", "Endpoint" };
            for (int i = 0; i < 3; i++) { Label caption = KeyboardUi.Label(UiText.T(labels[i]), 9, KeyboardUi.Muted); caption.Dock = DockStyle.Fill; grid.Controls.Add(caption, i, 0); }
            bus.Text = busId; bus.MaxLength = 31; bus.Dock = DockStyle.Fill; bus.Margin = new Padding(0, 0, 12, 0);
            product.DropDownStyle = ComboBoxStyle.DropDownList;
            product.Items.AddRange(new object[] { UiText.T("ProductConfirmed"), UiText.T("ProductExperimental") }); product.SelectedIndex = 0;
            product.Dock = DockStyle.Fill; product.Margin = new Padding(0, 0, 12, 0);
            endpoint.Minimum = 1; endpoint.Maximum = 15; endpoint.Value = 4; endpoint.Dock = DockStyle.Fill;
            grid.Controls.Add(bus, 0, 1); grid.Controls.Add(product, 1, 1); grid.Controls.Add(endpoint, 2, 1);
            Label note = KeyboardUi.Label(UiText.T("ExperimentalHint"), 9, KeyboardUi.Muted); note.Dock = DockStyle.Fill;
            grid.Controls.Add(note, 0, 2); grid.SetColumnSpan(note, 3);
            FlowLayoutPanel commands = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
            KeyboardUi.Button(start, UiText.T("StartRelay"), false); KeyboardUi.Button(stop, UiText.T("StopRelay"), false); stop.Enabled = false;
            start.Click += delegate { StartRelay(); };
            stop.Click += delegate {
                if (MessageBox.Show(this, UiText.T("StopConfirmMessage"), UiText.T("StopConfirmTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes) StopRelay();
            };
            commands.Controls.AddRange(new Control[] { start, stop }); grid.Controls.Add(commands, 0, 3); grid.SetColumnSpan(commands, 3);
            advanced.Controls.Add(grid);
        }

        private void SetController(Color color, string key) { controllerDot.DotColor = color; status.Text = UiText.T(key); }
        private void SetKeyboard(Color color, string key) { keyboardDot.DotColor = color; keyboardStatus.Text = UiText.T(key); }

        private void EditBindings()
        {
            if (preview || closing || editing) return;
            editing = true; SetEnabled(false); RefreshState();
            try
            {
                using (BindingEditor editor = new BindingEditor(profile))
                {
                    activeEditor = editor;
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    try { editor.Result.Save(profilePath); }
                    catch { MessageBox.Show(this, UiText.T("ProfileSaveFailure"), UiText.T("BindingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    profile = editor.Result; input.Reset();
                    for (int i = 0; i < BindingProfile.ActionCount; i++) bindingLabels[i].Text = UiText.KeyName(profile.KeyForAction(i));
                    notice.Text = UiText.T("BindingsPausedHint"); notice.ForeColor = KeyboardUi.Muted;
                }
            }
            finally { activeEditor = null; SetEnabled(false); editing = false; RefreshState(); }
        }

        private void InstallKeyboardHook()
        {
            hookCallback = KeyboardHook;
            hook = SetWindowsHookEx(13, hookCallback, GetModuleHandle(null), 0);
            hotkeyRegistered = hook != IntPtr.Zero && RegisterHotKey(Handle, ToggleHotkey, 0x4000, 0x77);
            if (!hotkeyRegistered)
            {
                start.Enabled = false;
                startupFailure = "Keyboard input setup failed";
                SetController(KeyboardUi.Red, "KeyboardSetupFailure");
                if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
                return;
            }
            for (int key = 0; key < 256; key++)
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                    input.Process(key, true, false, false, false, inputClock.ElapsedMilliseconds);
        }

        private void UpdateFocus()
        {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            long now = Stopwatch.GetTimestamp();
            if (pid == lastFocusPid && now - lastFocusCheck < Stopwatch.Frequency / 4) return;
            lastFocusPid = pid; lastFocusCheck = now;
            bool allowed = false;
            if (pid != 0 && pid != ownPid)
                try { using (Process p = Process.GetProcessById((int)pid))
                    allowed = String.Equals(p.MainModule.FileName, sonyPath, StringComparison.OrdinalIgnoreCase); }
                catch { }
            hookFocus = new FocusDecision(pid, allowed);
        }

        // No process queries, native merge, logging or network calls in the hook.
        private bool HookForeground()
        {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            FocusDecision decision = hookFocus;
            return pid != ownPid && decision.Pid == pid && decision.Allowed;
        }

        private static bool ModifiersDown()
        {
            return (GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        }

        private IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                KeyData key = (KeyData)Marshal.PtrToStructure(data, typeof(KeyData));
                bool down = message.ToInt64() == 0x100 || message.ToInt64() == 0x104;
                bool up = message.ToInt64() == 0x101 || message.ToInt64() == 0x105;
                bool modifiers = ModifiersDown();
                if (down && (key.Key == 0x11 || key.Key == 0x12 || key.Key == 0x5B || key.Key == 0x5C ||
                    key.Key == 0xA2 || key.Key == 0xA3 || key.Key == 0xA4 || key.Key == 0xA5)) modifiers = true;
                UsbIpRelay current = relay;
                bool capture = enabled && !closing && !editing && current != null && current.IsAttached && current.EndpointVerified &&
                    !modifiers && HookForeground();
                if (input.Process((int)key.Key, down, up, (key.Flags & 0x10) != 0, capture, inputClock.ElapsedMilliseconds))
                    return new IntPtr(1);
            }
            return CallNextHookEx(hook, code, message, data);
        }

        private byte[] MergePhysicalReport(byte[] physical)
        {
            // A pause may race one callback already in progress. Every following
            // callback takes a fresh enabled state and a fresh physical report;
            // no cached merged report is ever repeated after pause or release.
            bool active = enabled && !closing && !editing && !ModifiersDown() && HookForeground();
            uint held = input.Evaluate(active, inputClock.ElapsedMilliseconds);
            return PhysicalReportMerge.Merge(physical, held, active);
        }

        private RelayStatusSnapshot GetStatusSnapshot()
        {
            UsbIpRelay current = relay;
            return new RelayStatusSnapshot {
                Pid = (int)ownPid, Enabled = enabled,
                Attached = current != null && current.IsAttached,
                EndpointVerified = current != null && current.EndpointVerified,
                InputReports = current == null ? 0 : current.InputReports,
                MergedReports = current == null ? 0 : current.MergedReports,
                Failure = current == null ? startupFailure : current.Failure
            };
        }

        private void StartRelay()
        {
            if (preview || closing || relay != null || !hotkeyRegistered) return;
            SetEnabled(false);
            startupFailure = null;
            UsbIpRelay candidate = null;
            try
            {
                // Exercise the DLL before a physical device can attach. This sample
                // is never sent anywhere and never replaces a physical report.
                byte[] probe = new byte[64]; probe[0] = 1;
                PhysicalReportMerge.Merge(probe, 0, false);
                candidate = new UsbIpRelay(ListenPort, UpstreamPort, bus.Text.Trim(),
                    product.SelectedIndex == 0 ? (ushort)0x05C4 : (ushort)0x09CC,
                    Decimal.ToInt32(endpoint.Value), MergePhysicalReport);
                candidate.Start();
                relay = candidate; lastReportCount = 0; lastReportAt = Stopwatch.GetTimestamp();
                hadAttachment = false;
                start.Enabled = false; stop.Enabled = true;
                bus.Enabled = product.Enabled = endpoint.Enabled = false;
                SetController(KeyboardUi.Amber, "ControllerWaiting");
            }
            catch (Exception)
            {
                startupFailure = "Relay startup failed";
                if (candidate != null) candidate.Dispose();
                SetController(KeyboardUi.Red, "RelayStartupFailure");
            }
        }

        private void SetEnabled(bool value)
        {
            enabled = false;
            input.Reset();
            if (value && !closing && !editing && !preview) enabled = true;
            toggle.Text = UiText.T(enabled ? "PauseKeyboard" : "EnableKeyboard");
            if (!enabled) SetKeyboard(KeyboardUi.Gray, "KeyboardPaused");
        }

        private void Toggle()
        {
            UsbIpRelay current = relay;
            if (preview || closing || editing || !toggle.Enabled || current == null || !current.IsAttached || !current.EndpointVerified) return;
            SetEnabled(!enabled);
            RefreshState();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x312 && message.WParam.ToInt32() == ToggleHotkey)
            {
                if (editing && activeEditor != null) activeEditor.ReservedHotkeyPressed();
                else Toggle();
                return;
            }
            base.WndProc(ref message);
        }

        private void RefreshState()
        {
            if (preview || closing) return;
            UpdateFocus();
            bool foreground = HookForeground(), modifiers = ModifiersDown();
            if (!foreground || modifiers || editing) input.Reset();
            UsbIpRelay current = relay;
            if (current == null)
            {
                if (startupFailure != null) SetController(KeyboardUi.Red,
                    startupFailure == "Keyboard input setup failed" ? "KeyboardSetupFailure" : "RelayStartupFailure");
                else SetController(KeyboardUi.Gray, "ControllerDisconnected");
                SetKeyboard(KeyboardUi.Gray, "KeyboardPaused"); return;
            }
            long reports = current.InputReports;
            long now = Stopwatch.GetTimestamp();
            if (reports != lastReportCount) { lastReportCount = reports; lastReportAt = now; }
            if (current.Failure != null)
            {
                SetEnabled(false); toggle.Enabled = false;
                SetController(KeyboardUi.Red, "ControllerFailure");
                return;
            }
            if (!current.IsAttached)
            {
                SetEnabled(false); toggle.Enabled = false;
                SetController(hadAttachment ? KeyboardUi.Gray : KeyboardUi.Amber,
                    hadAttachment ? "ControllerDisconnected" : "ControllerWaiting");
                return;
            }
            hadAttachment = true;
            if (!current.EndpointVerified)
            {
                SetEnabled(false); toggle.Enabled = false;
                SetController(KeyboardUi.Amber, "ControllerVerifying");
                return;
            }
            bool fresh = reports > 0 && now - lastReportAt < Stopwatch.Frequency * 2;
            toggle.Enabled = fresh && !editing;
            if (!fresh)
            {
                SetEnabled(false);
                SetController(KeyboardUi.Amber, "ControllerStale");
            }
            else
            {
                SetController(KeyboardUi.Green, "ControllerReady");
                if (!enabled || editing) SetKeyboard(KeyboardUi.Gray, "KeyboardPaused");
                else if (!foreground || modifiers) SetKeyboard(KeyboardUi.Amber, "KeyboardFocusPaused");
                else SetKeyboard(KeyboardUi.Green, "KeyboardActive");
            }
        }

        private void StopRelay()
        {
            SetEnabled(false); toggle.Enabled = false;
            UsbIpRelay old = relay; relay = null;
            if (old != null) old.Dispose();
            stop.Enabled = false; start.Enabled = hotkeyRegistered;
            bus.Enabled = product.Enabled = endpoint.Enabled = true;
            if (!closing)
            {
                startupFailure = null;
                SetController(KeyboardUi.Gray, "ControllerDisconnected");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!preview && !closing && e.CloseReason == CloseReason.UserClosing && relay != null &&
                MessageBox.Show(this, UiText.T("StopConfirmMessage"), UiText.T("StopConfirmTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { e.Cancel = true; base.OnFormClosing(e); return; }
            closing = true; enabled = false; input.Reset(); if (timer != null) timer.Stop();
            if (hotkeyRegistered) UnregisterHotKey(Handle, ToggleHotkey);
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            StopRelay();
            if (statusFile != null) statusFile.Dispose();
            if (timer != null) timer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
