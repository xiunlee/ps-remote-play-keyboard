using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PSKeyboard
{
    // Separate experimental transport. This never opens a physical controller,
    // modifies Sony, starts/stops Sony, or installs a driver.
    internal sealed class DualSensePreview : Form
    {
        private const int ApiPort = 33642, UsbPort = 33641, ToggleHotkey = 0x504D;
        private const string UsbipHash = "817DA219FA153B4B37C3BC1AA8FCC63F33E046188C57CC3DD2C60A6EF0883C56";
        private const string UsbipDllHash = "F123EF5D288B817A890CBE39A3C2D212B3D8B1765C691C8703CCB76C7337986C";
        internal static readonly string[] Personas = { "dualsensecombinedaudioduplexv5", "dualsensegamepadv5", "dualsenseedgecombinedaudioduplexv5", "dualsenseedgegamepadv5" };
        private static readonly string[] PersonaLabels = { "1 · DualSense 一般版／含音訊介面", "2 · DualSense 一般版／純手把", "3 · DualSense Edge／含音訊介面", "4 · DualSense Edge／純手把" };
        private readonly object sessionGate = new object(), logGate = new object(), focusGate = new object();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly KeyboardControlPipeline input;
        private volatile BindingProfile profile;
        private readonly string profilePath, statusPath;
        private readonly string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string sonyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sony", "PS Remote Play", "RemotePlay.exe");
        private readonly string usbipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "USBip", "usbip.exe");
        private readonly int ownPid = Process.GetCurrentProcess().Id;
        private readonly Button connect = new Button(), toggle = new Button(), configure = new Button();
        private readonly ComboBox controllerChoice = new ComboBox();
        private readonly bool preview;
        private readonly StatusDot connectionDot = new StatusDot(), keyboardDot = new StatusDot();
        private readonly Label[] keySummaries = new Label[4];
        private string controllerType;
        private readonly Label status = new Label(), keyboardStatus = new Label(), notice = new Label();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly ManualResetEvent statusStop = new ManualResetEvent(false);
        private Thread sender, statusWorker;
        private volatile CandidateTransport transport;
        private Process server;
        private volatile int serverPid, attachedPort, unconfirmedUsbipCommandPid;
        private volatile string attachedBusId, failureCode, cleanupCode;
        private volatile bool enabled, closing, editing, busy, usbAttachedVerified, attachAttempted, usbipCommandTerminationUnconfirmed, cleanupBlocked, attachReturned;
        private long framesSent, nonNeutralFramesSent, stdoutLines, stderrLines, featureWarnings;
        private string receiptPath, stdoutPath, stderrPath, sessionId;
        private DateTime serverStartedUtc;
        private IntPtr hook;
        private HookCallback callback;
        private bool hotkeyRegistered;
        private uint focusedPid;
        private long focusedAt;
        private sealed class FocusDecision
        {
            internal readonly uint Pid; internal readonly bool Allowed;
            internal FocusDecision(uint pid, bool allowed) { Pid = pid; Allowed = allowed; }
        }
        private volatile FocusDecision focus = new FocusDecision(0, false);

        private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct KeyData { internal uint Key, Scan, Flags, Time; internal UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookCallback callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--render-preview") return RenderPreview(args);
            bool autoConnect; string statusFile, deviceType;
            try { ParseOptions(args, out autoConnect, out statusFile, out deviceType); }
            catch (ArgumentException)
            {
                MessageBox.Show("用法：PSKeyboard-DualSensePreview.exe [--persona 候選名稱] [--status-file 本測試版目錄內的完整 JSON 路徑]\r\n開啟後請自行選擇方案並連接，F8 啟用鍵盤。", "上班不要玩 " + BuildInfo.Version); return 2;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\PSKeyboardControllerFreeExperiment", out created))
            {
                if (!created) { MessageBox.Show("另一版「上班不要玩」仍在執行。\r\n請先正常關閉它，再開本測試版；Sony Remote Play 請保持開啟。", "上班不要玩"); return 1; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DualSensePreview(autoConnect, statusFile, deviceType, false));
            }
            return 0;
        }

        internal static void ParseOptions(string[] args, out bool autoConnect, out string statusFile)
        {
            string ignored; ParseOptions(args, out autoConnect, out statusFile, out ignored);
        }
        internal static void ParseOptions(string[] args, out bool autoConnect, out string statusFile, out string deviceType)
        {
            autoConnect = false; statusFile = null; deviceType = Personas[0]; bool typeSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--status-file" && statusFile == null && i + 1 < args.Length) statusFile = ValidateStatusPath(args[++i]);
                else if (args[i] == "--persona" && !typeSet && i + 1 < args.Length)
                {
                    deviceType = args[++i];
                    if (Array.IndexOf(Personas, deviceType) < 0) throw new ArgumentException("Unknown candidate persona.");
                    typeSet = true;
                }
                else throw new ArgumentException("Invalid options.");
            }
        }

        internal static string ValidateStatusPath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length < 4 || !Char.IsLetter(path[0]) || path[1] != ':' ||
                (path[2] != '\\' && path[2] != '/') || path.IndexOf(':', 2) >= 0 ||
                !String.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected a local JSON path.");
            string result = Path.GetFullPath(path);
            string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\') + "\\";
            if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Status must remain inside this candidate package.");
            if (!Directory.Exists(Path.GetDirectoryName(result))) throw new ArgumentException("Status directory must already exist.");
            return result;
        }

        private static int RenderPreview(string[] args)
        {
            try
            {
                if (args.Length != 2 && args.Length != 4) return 2;
                string path = args[1];
                if (String.IsNullOrWhiteSpace(path) || path.Length < 4 || !Char.IsLetter(path[0]) || path[1] != ':' ||
                    (path[2] != '\\' && path[2] != '/') || path.IndexOf(':', 2) >= 0 ||
                    !String.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) return 2;
                path = Path.GetFullPath(path);
                if (!Directory.Exists(Path.GetDirectoryName(path))) return 2;
                if (args.Length == 4)
                {
                    if (args[2] != "--preview-language") return 2;
                    UiText.Initialize(CultureInfo.GetCultureInfo(args[3]));
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (Form form = new DualSensePreview(false, null, Personas[0], true))
                {
                    CreatePreviewControls(form); form.PerformLayout();
                    form.Opacity = 0; form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-32000, -32000); form.Show(); form.Update(); Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
                    form.Hide();
                }
                return 0;
            }
            catch { return 2; }
        }

        private static void CreatePreviewControls(Control control)
        {
            IntPtr handle = control.Handle;
            foreach (Control child in control.Controls) CreatePreviewControls(child);
            control.PerformLayout();
        }
        protected override bool ShowWithoutActivation { get { return preview; } }

        private DualSensePreview(bool autoConnect, string statusFile, string deviceType, bool renderPreview)
        {
            preview = renderPreview; statusPath = preview ? null : statusFile; controllerType = deviceType;
            profilePath = preview ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSKeyboard", "bindings.json");
            profile = BindingProfile.Default(); bool badProfile = false;
            if (!preview) try { profile = BindingProfile.Load(profilePath); } catch { badProfile = true; }
            input = new KeyboardControlPipeline(delegate { return profile; });
            Text = "上班不要玩 " + BuildInfo.Version + " · DualSense 相容性測試"; ClientSize = new Size(980, 710); MinimumSize = new Size(940, 720);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = KeyboardUi.Background; ForeColor = KeyboardUi.Ink; Font = KeyboardUi.Font(10, FontStyle.Regular);
            Icon brandIcon = KeyboardUi.LoadAppIcon(); if (brandIcon != null) { Icon = brandIcon; Disposed += delegate { brandIcon.Dispose(); }; }
            BuildInterface(badProfile);
            if (preview)
            {
                status.Text = "尚未連接\r\n選一種方案，逐一測試。"; connectionDot.DotColor = KeyboardUi.Gray;
                keyboardStatus.Text = "已暫停\r\n連接後按 F8 啟用。"; keyboardDot.DotColor = KeyboardUi.Gray;
                return;
            }
            Shown += delegate {
                callback = KeyboardHook; hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                hotkeyRegistered = hook != IntPtr.Zero && RegisterHotKey(Handle, ToggleHotkey, 0x4000, 0x77);
                if (!hotkeyRegistered)
                {
                    failureCode = "keyboard_setup_failed"; connect.Enabled = false;
                    status.Text = "F8 或鍵盤擷取無法啟用。請先關閉其他版本的「上班不要玩」，保留 Sony Remote Play。";
                    if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
                }
                else
                {
                    for (int key = 0; key < 256; key++)
                        if ((GetAsyncKeyState(key) & 0x8000) != 0) input.Process(key, true, false, false, false, clock.ElapsedMilliseconds);
                    // A package launch never creates a device; connection is an explicit UI action.
                }
                StartStatusWriter();
            };
            uiTimer.Interval = 100; uiTimer.Tick += delegate { RefreshStatus(); }; uiTimer.Start();
        }

        private void BuildInterface(bool badProfile)
        {
            TableLayoutPanel shell = KeyboardUi.Table(1); shell.RowCount = 2;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 140)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(shell);
            TableLayoutPanel hero = KeyboardUi.Table(3); hero.BackColor = KeyboardUi.Accent; hero.Padding = new Padding(28, 20, 28, 20);
            hero.Paint += delegate(object sender, PaintEventArgs e) {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen line = new Pen(Color.FromArgb(24, Color.White), 1))
                    for (int i = 0; i < 4; i++) e.Graphics.DrawLines(line, new Point[] {
                        new Point(hero.Width - 340 + i * 42, 0), new Point(hero.Width - 430 + i * 42, hero.Height),
                        new Point(hero.Width - 180 + i * 42, hero.Height) });
            };
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            Image mark = KeyboardUi.LoadBrandMark();
            PictureBox brand = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = mark, Margin = new Padding(0, 0, 18, 0) };
            if (mark != null) brand.Disposed += delegate { mark.Dispose(); };
            hero.Controls.Add(brand, 0, 0);
            TableLayoutPanel wordmark = KeyboardUi.Table(1); wordmark.RowCount = 3;
            wordmark.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); wordmark.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); wordmark.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label eyebrow = KeyboardUi.Label("KEYBOARD / REMOTE PLAY", 9, Color.FromArgb(255, 216, 229)); eyebrow.Font = KeyboardUi.CodeFont(9, FontStyle.Regular); eyebrow.Dock = DockStyle.Fill;
            Label title = KeyboardUi.Label("上班不要玩", 27, Color.White); title.Font = KeyboardUi.Font(27, FontStyle.Bold); title.Dock = DockStyle.Fill;
            Label subtitle = KeyboardUi.Label("DualSense 相容性測試版 · Remote Play 操作尚待確認", 10, Color.White); subtitle.Dock = DockStyle.Fill;
            wordmark.Controls.Add(eyebrow, 0, 0); wordmark.Controls.Add(title, 0, 1); wordmark.Controls.Add(subtitle, 0, 2); hero.Controls.Add(wordmark, 1, 0);
            Label version = KeyboardUi.Label("TEST / 4 OPTIONS\r\nv" + BuildInfo.Version, 9, Color.White); version.Font = KeyboardUi.CodeFont(9, FontStyle.Regular); version.Dock = DockStyle.Fill; version.TextAlign = ContentAlignment.MiddleRight; hero.Controls.Add(version, 2, 0);
            shell.Controls.Add(hero, 0, 0);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 22, 28, 18), Margin = Padding.Empty };
            TableLayoutPanel body = KeyboardUi.Table(1); body.Dock = DockStyle.Top; body.AutoSize = true; body.AutoSizeMode = AutoSizeMode.GrowAndShrink; body.RowCount = 6;
            foreach (int height in new int[] { 70, 110, 64, 138, 72, 42 }) body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            scroll.Controls.Add(body); shell.Controls.Add(scroll, 0, 1);
            TableLayoutPanel mode = KeyboardUi.Table(2); mode.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); mode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Label modeTitle = KeyboardUi.Label("測試方案", 12, KeyboardUi.Ink); modeTitle.Font = KeyboardUi.Font(12, FontStyle.Bold); modeTitle.Dock = DockStyle.Fill;
            controllerChoice.DropDownStyle = ComboBoxStyle.DropDownList; controllerChoice.Width = 470; controllerChoice.Anchor = AnchorStyles.Left;
            controllerChoice.FlatStyle = FlatStyle.Flat; controllerChoice.BackColor = KeyboardUi.Surface; controllerChoice.ForeColor = KeyboardUi.Ink;
            controllerChoice.Items.AddRange(PersonaLabels);
            controllerChoice.SelectedIndex = Array.IndexOf(Personas, controllerType);
            controllerChoice.SelectedIndexChanged += delegate { controllerType = Personas[controllerChoice.SelectedIndex]; };
            mode.Controls.Add(modeTitle, 0, 0); mode.Controls.Add(controllerChoice, 1, 0); body.Controls.Add(mode, 0, 0);
            TableLayoutPanel states = KeyboardUi.Table(2); states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            states.Controls.Add(StatusCard("手把連線", status, connectionDot, new Padding(0, 0, 12, 12)), 0, 0);
            states.Controls.Add(StatusCard("鍵盤控制", keyboardStatus, keyboardDot, new Padding(0, 0, 0, 12)), 1, 0); body.Controls.Add(states, 0, 1);
            status.Text = "尚未連接\r\n選一種方案，逐一測試。"; keyboardStatus.Text = "已暫停\r\n連接後按 F8 啟用。";
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
            KeyboardUi.Button(toggle, "啟用鍵盤 · F8", true); KeyboardUi.Button(configure, "查看按鍵設定", false); KeyboardUi.Button(connect, "連接此方案", false);
            toggle.Enabled = false; connect.Click += ConnectClick; toggle.Click += delegate { Toggle(); }; configure.Click += delegate { EditBindings(); };
            actions.Controls.AddRange(new Control[] { toggle, configure, connect }); body.Controls.Add(actions, 0, 2);
            TableLayoutPanel guide = KeyboardUi.Table(4); guide.BackColor = KeyboardUi.Surface; guide.Padding = new Padding(18, 12, 18, 12); guide.Margin = new Padding(0, 0, 0, 12); guide.RowCount = 2;
            guide.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); guide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label guideTitle = KeyboardUi.Label("你的按鍵配置", 11, KeyboardUi.Ink); guideTitle.Font = KeyboardUi.Font(11, FontStyle.Bold); guideTitle.Dock = DockStyle.Fill;
            guide.Controls.Add(guideTitle, 0, 0); guide.SetColumnSpan(guideTitle, 4);
            for (int i = 0; i < 4; i++) { guide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); keySummaries[i] = KeyboardUi.Label("", 10, KeyboardUi.Ink); keySummaries[i].Dock = DockStyle.Fill; guide.Controls.Add(keySummaries[i], i, 1); }
            UpdateBindingSummary(); body.Controls.Add(guide, 0, 3);
            Label help = KeyboardUi.Label("1 選方案並連接 → 2 切回官方 Remote Play，按 F8 → 3 測試方向與確認鍵\r\nF8 暫停；切窗停止。測下一方案前，先在這裡中斷目前手把。\r\n四種方案逐一測試；回退 PS4 請使用保留的 alpha.8。", 9, KeyboardUi.Muted); help.Dock = DockStyle.Fill; body.Controls.Add(help, 0, 4);
            notice.Text = badProfile ? "原設定檔讀取失敗，暫用預設；原檔保留不變。" : "沿用既有個人鍵位與連點，這個測試版只讀設定。Windows 通過不代表 PS5 操作成功。";
            notice.ForeColor = badProfile ? KeyboardUi.Amber : KeyboardUi.Muted; notice.Font = KeyboardUi.Font(9, FontStyle.Regular); notice.Dock = DockStyle.Fill; notice.TextAlign = ContentAlignment.MiddleLeft; body.Controls.Add(notice, 0, 5);
        }

        private static Control StatusCard(string heading, Label value, StatusDot dot, Padding margin)
        {
            TableLayoutPanel card = KeyboardUi.Table(2); card.BackColor = KeyboardUi.Surface; card.Padding = new Padding(16, 10, 14, 10); card.Margin = margin; card.RowCount = 2;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dot.Margin = new Padding(0, 3, 0, 0); card.Controls.Add(dot, 0, 0);
            Label title = KeyboardUi.Label(heading, 9, KeyboardUi.Muted); title.Dock = DockStyle.Fill; card.Controls.Add(title, 1, 0);
            value.Dock = DockStyle.Fill; value.Font = KeyboardUi.Font(10, FontStyle.Regular); value.TextAlign = ContentAlignment.MiddleLeft; value.Margin = Padding.Empty;
            card.Controls.Add(value, 1, 1); return card;
        }
        private string KeyNames(params int[] actions)
        {
            string[] names = new string[actions.Length]; for (int i = 0; i < actions.Length; i++) names[i] = UiText.KeyName(profile.KeyForAction(actions[i]));
            return String.Join(" / ", names);
        }
        private void UpdateBindingSummary()
        {
            keySummaries[0].Text = "左搖桿\r\n" + KeyNames(0, 2, 1, 3);
            keySummaries[1].Text = "△  □  ×  ○\r\n" + KeyNames(11, 10, 8, 9);
            keySummaries[2].Text = "L1 / L2\r\n" + KeyNames(12, 14);
            keySummaries[3].Text = "R1 / R2\r\n" + KeyNames(13, 15);
        }

        private void UpdateFocus()
        {
            lock (focusGate)
            {
                uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                long now = clock.ElapsedMilliseconds;
                if (pid == focusedPid && now - focusedAt < 250) return;
                focusedPid = pid; focusedAt = now; bool allowed = false;
                if (pid != 0) try { using (Process process = Process.GetProcessById((int)pid)) allowed = String.Equals(process.MainModule.FileName, sonyPath, StringComparison.OrdinalIgnoreCase); } catch { }
                focus = new FocusDecision(pid, allowed);
            }
        }
        private bool SonyForeground()
        {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            FocusDecision decision = focus; return decision.Pid == pid && decision.Allowed;
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
                bool down = message.ToInt64() == 0x100 || message.ToInt64() == 0x104, up = message.ToInt64() == 0x101 || message.ToInt64() == 0x105;
                bool modifiers = ModifiersDown();
                if (down && (key.Key == 0x11 || key.Key == 0x12 || key.Key == 0x5B || key.Key == 0x5C || key.Key == 0xA2 || key.Key == 0xA3 || key.Key == 0xA4 || key.Key == 0xA5)) modifiers = true;
                CandidateTransport current = transport;
                bool capture = enabled && !closing && !editing && usbAttachedVerified && current != null && current.IsConnected && !modifiers && SonyForeground();
                if (input.Process((int)key.Key, down, up, (key.Flags & 0x10) != 0, capture, clock.ElapsedMilliseconds)) return new IntPtr(1);
            }
            return CallNextHookEx(hook, code, message, data);
        }
        private void SetEnabled(bool value)
        {
            enabled = false; input.Reset();
            if (value && !closing && !editing) enabled = true;
            toggle.Text = enabled ? "暫停鍵盤 · F8" : "啟用鍵盤 · F8";
        }
        private void Toggle()
        {
            CandidateTransport current = transport;
            if (closing || editing || !toggle.Enabled || !usbAttachedVerified || current == null || !current.IsConnected) return;
            SetEnabled(!enabled); RefreshStatus();
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x312 && message.WParam.ToInt32() == ToggleHotkey)
            {
                Toggle(); return;
            }
            base.WndProc(ref message);
        }
        private void EditBindings()
        {
            if (closing || editing || busy) return;
            editing = true; SetEnabled(false);
            try
            {
                StringBuilder text = new StringBuilder("目前設定（唯讀）\r\nF8：啟用／暫停\r\n\r\n");
                for (int action = 0; action < BindingProfile.ActionCount; action++)
                {
                    RapidFireMode mode = profile.RapidFireModeForAction(action);
                    text.Append(UiText.ActionName(action)).Append("  →  ").Append(UiText.KeyName(profile.KeyForAction(action)));
                    if (mode != RapidFireMode.Off) text.Append(mode == RapidFireMode.Hold ? "　按住連點 " : "　切換連點 ").Append(profile.IntervalMillisecondsForAction(action)).Append(" ms");
                    text.AppendLine();
                }
                text.AppendLine("\r\n測試版不會修改個人設定。若需改鍵，請在保留的 alpha.8 設定後再開本測試版。");
                using (Form dialog = new Form { Text = "按鍵與連點（唯讀）", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(580, 620), MinimizeBox = false, MaximizeBox = false })
                using (TextBox content = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = text.ToString(), Font = KeyboardUi.Font(10, FontStyle.Regular), BackColor = KeyboardUi.Surface })
                { dialog.Controls.Add(content); dialog.ShowDialog(this); }
            }
            finally { SetEnabled(false); editing = false; RefreshStatus(); }
        }
        private void RefreshStatus()
        {
            if (closing) return;
            UpdateFocus(); bool foreground = SonyForeground();
            if (!foreground || ModifiersDown() || editing) input.Reset();
            CandidateTransport current = transport;
            if (current != null && !current.IsConnected) { enabled = false; input.Reset(); toggle.Enabled = false; }
            if (failureCode != null)
            {
                SetEnabled(false); toggle.Enabled = false;
                status.Text = FailureMessage(failureCode) + (cleanupCode == null ? "" : "\r\n清理未完全確認，已鎖住重新連接。");
            }
            else if (cleanupBlocked) status.Text = "清理未完全確認，已鎖住重新連接。\r\n請保留本機診斷，不要重複建立裝置。";
            connectionDot.DotColor = failureCode != null ? KeyboardUi.Red : busy ? KeyboardUi.Amber : usbAttachedVerified && current != null && current.IsConnected ? KeyboardUi.Green : KeyboardUi.Gray;
            keyboardDot.DotColor = !enabled ? KeyboardUi.Gray : !foreground || ModifiersDown() ? KeyboardUi.Amber : KeyboardUi.Accent;
            keyboardStatus.Text = !enabled ? "已暫停\r\n按 F8 開始控制。" : !foreground || ModifiersDown() ? "等待遊戲視窗\r\n切回 Remote Play 即可操作。" : "鍵盤控制中\r\n按 F8 隨時暫停。";
        }
        private static string FailureMessage(string code)
        {
            switch (code)
            {
                case "keyboard_setup_failed": return "F8 或鍵盤擷取無法啟用，請先關閉其他版本的鍵盤程式。";
                case "usbip_missing": return "找不到已安裝的 USBip；本程式不會自動安裝驅動。";
                case "usbip_invalid": return "USBip 0.9.8.1 元件驗證失敗，沒有更換或降級驅動。";
                case "existing_import": return "發現先前測試通道仍有接入，已停止建立新裝置。";
                case "backend_invalid": return "候選後端遺失或驗證失敗。";
                case "ports_unavailable": return "實驗通道正由其他程式使用，沒有啟動新手把。";
                case "usb_attach_failed": return "USB 手把接入未能確認；尚未啟用鍵盤。";
                case "send_failed": return "虛擬手把輸出通道已中斷，鍵盤已暫停。";
                case "cancelled": return "操作已取消。";
                default: return "建立實驗手把失敗。請查看本機診斷狀態。";
            }
        }

        private void UpdateUi(Action action)
        { if (!closing && IsHandleCreated) try { BeginInvoke(action); } catch (InvalidOperationException) { } }
        internal static bool CanReconnect(bool cleanupUnknown, bool attachUnresolved, bool commandUnconfirmed)
        { return !cleanupUnknown && !attachUnresolved && !commandUnconfirmed; }
        internal static bool HasCandidateImport(string listing)
        { return (listing ?? "").IndexOf("usbip://127.0.0.1:" + UsbPort + "/", StringComparison.OrdinalIgnoreCase) >= 0; }
        private string SelectedLabel() { return PersonaLabels[Array.IndexOf(Personas, controllerType)]; }
        private void ConnectClick(object senderObject, EventArgs args)
        {
            if (closing || editing || busy || !hotkeyRegistered) return;
            if (transport == null && server == null && !CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed))
            { status.Text = "上次 USB 移除尚未確認，暫不建立新裝置；請先核對本機診斷中的接入位置。"; return; }
            if (transport != null || server != null)
            {
                SetEnabled(false); busy = true; connect.Enabled = toggle.Enabled = configure.Enabled = false;
                ThreadPool.QueueUserWorkItem(delegate { Cleanup(); UpdateUi(delegate { busy = false; configure.Enabled = true; connect.Enabled = controllerChoice.Enabled = CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed); connect.Text = "連接此方案"; status.Text = cleanupCode == null ? "手把已中斷，可選下一方案。\r\n鍵盤保持暫停，Sony 仍維持原狀。" : "清理未完全確認，已鎖住重新連接。"; }); });
                return;
            }
            busy = true; connect.Enabled = configure.Enabled = controllerChoice.Enabled = false; failureCode = cleanupCode = null; attachReturned = false; SetEnabled(false); status.Text = "正在連接…\r\n" + SelectedLabel();
            ThreadPool.QueueUserWorkItem(delegate {
                lock (sessionGate)
                {
                    try
                    {
                        CheckClosing();
                        if (!File.Exists(usbipPath)) throw new ExperimentException("usbip_missing");
                        if (!ValidHash(usbipPath, UsbipHash) || !ValidHash(Path.Combine(Path.GetDirectoryName(usbipPath), "libusbip.dll"), UsbipDllHash)) throw new ExperimentException("usbip_invalid");
                        if (HasCandidateImport(Usbip("port"))) { NoteCleanupFailure("existing_import"); throw new ExperimentException("existing_import"); }
                        string executable = Path.Combine(baseDir, "DualSenseCandidateServer.exe");
                        if (!ValidBackend(executable)) throw new ExperimentException("backend_invalid");
                        if (ListenerPid(ApiPort) != 0 || ListenerPid(UsbPort) != 0) throw new ExperimentException("ports_unavailable");
                        InitializeDiagnostics();
                        ProcessStartInfo launch = new ProcessStartInfo(executable, "--candidate-preview") {
                            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                            WorkingDirectory = baseDir, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                        List<string> inherited = new List<string>();
                        foreach (string key in launch.EnvironmentVariables.Keys) if (key.StartsWith("VIIPER", StringComparison.OrdinalIgnoreCase)) inherited.Add(key);
                        foreach (string key in inherited) launch.EnvironmentVariables.Remove(key);
                        server = new Process { StartInfo = launch };
                        server.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) RecordServerLine("stdout", e.Data); };
                        server.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) RecordServerLine("stderr", e.Data); };
                        if (!server.Start()) throw new ExperimentException("backend_start_failed");
                        serverPid = server.Id; serverStartedUtc = server.StartTime.ToUniversalTime(); WriteReceipt("running", null);
                        server.BeginOutputReadLine(); server.BeginErrorReadLine();
                        bool ready = false;
                        for (int attempt = 0; attempt < 40; attempt++)
                        {
                            CheckClosing(); if (server.HasExited) throw new ExperimentException("backend_exited");
                            if (ListenerPid(ApiPort) == serverPid && ListenerPid(UsbPort) == serverPid) { ready = true; break; }
                            Thread.Sleep(100);
                        }
                        if (!ready) throw new ExperimentException("ports_unavailable");
                        CandidateTransport candidate = new CandidateTransport(ApiPort, controllerType); transport = candidate; candidate.Connect();
                        if (candidate.ServerVersion != "research.preview.1") throw new ExperimentException("backend_invalid");
                        CheckClosing(); attachedBusId = candidate.UsbBusId;
                        attachAttempted = true; WriteReceipt("attach_requested", null);
                        string output = Usbip("--tcp-port " + UsbPort + " attach --remote 127.0.0.1 --bus-id " + attachedBusId + " --once --terse");
                        attachReturned = true;
                        int parsed;
                        if (!Int32.TryParse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || parsed <= 0) throw new ExperimentException("usb_attach_failed");
                        attachedPort = parsed;
                        if (!OwnPortUrl(Usbip("port " + parsed), attachedBusId)) throw new ExperimentException("usb_attach_failed");
                        usbAttachedVerified = true; WriteReceipt("attached", null); CheckClosing();
                        sender = new Thread(SendLoop) { IsBackground = true, Name = "Controller-free state sender" }; sender.Start();
                        UpdateUi(delegate { busy = false; connect.Enabled = configure.Enabled = toggle.Enabled = true; connect.Text = "中斷此方案"; status.Text = "Windows 接入已確認，PS5 尚待操作。\r\n" + SelectedLabel(); });
                    }
                    catch (Exception ex)
                    {
                        ExperimentException expected = ex as ExperimentException;
                        failureCode = expected == null ? "connect_failed" : expected.Code;
                        RecordDiagnostic("connect_failure", ex.GetType().Name); Cleanup();
                        UpdateUi(delegate { busy = false; configure.Enabled = true; connect.Enabled = controllerChoice.Enabled = CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed); connect.Text = "連接此方案"; RefreshStatus(); });
                    }
                }
            });
        }
        private void CheckClosing() { if (closing) throw new ExperimentException("cancelled"); }
        private static bool ValidBackend(string path)
        { return ValidHash(path, BuildInfo.BackendSha256); }
        private static bool ValidHash(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
                return String.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase);
        }

        // Read-only ownership lookup prevents connecting to somebody else's
        // server when a port is occupied or raced during startup.
        private static int ListenerPid(int port)
        {
            int size = 0; uint result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
            if (result != 122 || size < 4) throw new IOException("TCP owner query failed.");
            IntPtr table = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(table, ref size, false, 2, 3, 0) != 0) throw new IOException("TCP owner query failed.");
                int count = Marshal.ReadInt32(table);
                if (count < 0 || count > (size - 4) / 24) throw new IOException("TCP owner table was invalid.");
                for (int row = 0; row < count; row++)
                {
                    IntPtr entry = IntPtr.Add(table, 4 + row * 24);
                    int localPort = Marshal.ReadByte(entry, 8) * 256 + Marshal.ReadByte(entry, 9);
                    if (localPort == port) return Marshal.ReadInt32(entry, 20);
                }
                return 0;
            }
            finally { Marshal.FreeHGlobal(table); }
        }
        internal static bool OwnPortUrl(string detail, string busId)
        {
            return !String.IsNullOrEmpty(busId) && Regex.IsMatch(busId, @"\A[0-9]+-[0-9]+\z") &&
                Regex.IsMatch(detail ?? "", @"(?m)^\s*-> usbip://127\.0\.0\.1:" + UsbPort + "/" + Regex.Escape(busId) + @"\s*$");
        }
        internal static int ResolveOwnedPort(string listing, string busId)
        {
            if (String.IsNullOrEmpty(busId) || !Regex.IsMatch(busId, @"\A[0-9]+-[0-9]+\z")) throw new ArgumentException("Invalid own bus.");
            string expected = "-> usbip://127.0.0.1:" + UsbPort + "/" + busId;
            int current = 0, found = 0;
            foreach (string line in (listing ?? "").Split('\n'))
            {
                Match section = Regex.Match(line, @"^\s*Port\s+([0-9]+):");
                if (section.Success)
                {
                    if (!Int32.TryParse(section.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out current) || current <= 0) current = 0;
                }
                else if (Regex.IsMatch(line, @"^\s*Port\b")) current = 0;
                if (!String.Equals(line.Trim(), expected, StringComparison.Ordinal)) continue;
                if (found != 0 || current <= 0) throw new InvalidDataException("Own USBip port is ambiguous.");
                found = current;
            }
            return found;
        }
        private string Usbip(string arguments)
        {
            using (Process process = new Process { StartInfo = new ProcessStartInfo(usbipPath, arguments) {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true } })
            {
                StringBuilder output = new StringBuilder(); bool overflow = false;
                process.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) lock (output) { if (output.Length + e.Data.Length + 2 <= 32768) output.AppendLine(e.Data); else overflow = true; } };
                // Do not put raw tool errors or USB data into persistent logs.
                process.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) RecordServerLine("usbip", e.Data); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); if (!process.WaitForExit(2500)) MarkUnconfirmedUsbipCommand(process.Id); }
                    catch { MarkUnconfirmedUsbipCommand(process.Id); }
                    throw new ExperimentException("usbip_timeout");
                }
                process.WaitForExit();
                if (process.ExitCode != 0) throw new ExperimentException("usb_attach_failed");
                if (overflow) throw new ExperimentException("usbip_output_limit");
                return output.ToString();
            }
        }
        private void MarkUnconfirmedUsbipCommand(int pid)
        {
            // The command may still complete an attachment later. Its PID is
            // diagnostic evidence only; never use a reused PID to kill later.
            unconfirmedUsbipCommandPid = pid;
            usbipCommandTerminationUnconfirmed = true;
            NoteCleanupFailure("usbip_command_stop_unconfirmed");
        }
        private void SendLoop()
        {
            CandidateTransport session = transport;
            try
            {
                while (!closing && session != null && Object.ReferenceEquals(transport, session) && session.IsConnected)
                {
                    UpdateFocus(); bool active = enabled && !editing && usbAttachedVerified && !ModifiersDown() && SonyForeground();
                    uint actions = input.Evaluate(active, clock.ElapsedMilliseconds);
                    byte[] frame = ViiperTransport.ConvertKeyboardFrame(KeyboardMapping.Frame(actions, active), session.DeviceType); session.Send(frame);
                    Interlocked.Increment(ref framesSent); if (!IsNeutralFrame(frame)) Interlocked.Increment(ref nonNeutralFramesSent);
                    Thread.Sleep(8);
                }
                if (!closing && Object.ReferenceEquals(transport, session)) throw new IOException("Device stream ended.");
            }
            catch (Exception ex)
            {
                enabled = false; input.Reset();
                if (session != null && session.IsConnected) try { session.SendNeutral(); } catch { NoteCleanupFailure("neutral_unconfirmed"); }
                if (!closing && Object.ReferenceEquals(transport, session)) { failureCode = "send_failed"; RecordDiagnostic("send_failure", ex.GetType().Name); }
            }
        }
        internal static bool IsNeutralFrame(byte[] frame)
        {
            if (frame == null || (frame.Length != 31 && frame.Length != 33)) return false;
            for (int i = 0; i < frame.Length; i++) if (frame[i] != (i == frame.Length - 1 ? 0xE0 : 0)) return false;
            return true;
        }
        private void Cleanup()
        {
            lock (sessionGate)
            {
                enabled = false; input.Reset(); usbAttachedVerified = false;
                CandidateTransport old = transport; transport = null;
                if (sender != null && sender != Thread.CurrentThread && !sender.Join(2500)) NoteCleanupFailure("sender_stop_unconfirmed");
                sender = null;
                if (old != null && old.IsConnected) try { old.SendNeutral(); } catch { NoteCleanupFailure("neutral_unconfirmed"); }
                bool attachmentCleared = !attachAttempted && !usbipCommandTerminationUnconfirmed;
                // A timeout/failed attach can complete later inside the driver even
                // after its CLI exits. An empty immediate port list cannot clear this latch.
                if (attachAttempted && !attachReturned) NoteCleanupFailure("attach_command_incomplete");
                if (attachAttempted && attachedBusId != null && server != null)
                {
                    try
                    {
                        int listenerOwner = ListenerPid(UsbPort);
                        if (listenerOwner != 0 && listenerOwner != serverPid) throw new IOException("Server ownership changed.");
                        // Even an attach command with missing/invalid output may
                        // have imported a device. Discover only our exact URL.
                        int owned = ResolveOwnedPort(Usbip("port"), attachedBusId);
                        if (owned != 0)
                        {
                            attachedPort = owned;
                            if (!OwnPortUrl(Usbip("port " + owned), attachedBusId)) throw new IOException("Port ownership changed.");
                            listenerOwner = ListenerPid(UsbPort);
                            if (listenerOwner != 0 && listenerOwner != serverPid) throw new IOException("Server ownership changed.");
                            Usbip("detach --port " + owned);
                        }
                        attachmentCleared = ResolveOwnedPort(Usbip("port"), attachedBusId) == 0 && !usbipCommandTerminationUnconfirmed;
                        if (!attachmentCleared) NoteCleanupFailure("detach_unconfirmed");
                    }
                    catch { NoteCleanupFailure("detach_unconfirmed"); }
                }
                if (usbipCommandTerminationUnconfirmed) { attachmentCleared = false; NoteCleanupFailure("usbip_command_stop_unconfirmed"); }
                if (attachmentCleared) { attachedPort = 0; attachAttempted = false; }
                if (old != null && attachmentCleared)
                {
                    try { old.Dispose(); if (old.BusId != 0) NoteCleanupFailure("bus_cleanup_unconfirmed"); }
                    catch { NoteCleanupFailure("bus_cleanup_unconfirmed"); }
                }
                else if (old != null) NoteCleanupFailure("bus_cleanup_blocked_by_port");
                int? exitCode = null;
                if (server != null)
                {
                    try
                    {
                        if (serverPid != 0)
                        {
                            if (!server.HasExited)
                            {
                                server.StandardInput.WriteLine("stop"); server.StandardInput.Flush(); server.StandardInput.Close();
                                if (!server.WaitForExit(5000))
                                {
                                    NoteCleanupFailure("server_forced_stop"); server.Kill();
                                    if (!server.WaitForExit(2500)) NoteCleanupFailure("server_stop_unconfirmed");
                                }
                            }
                            if (server.HasExited) { exitCode = server.ExitCode; if (exitCode != 0) NoteCleanupFailure("server_exit_failed"); }
                        }
                    }
                    catch
                    {
                        NoteCleanupFailure("server_stop_unconfirmed");
                        // Only this Process object is owned; never look up a process
                        // by name or kill a PID saved in an older receipt.
                        try { if (serverPid != 0 && !server.HasExited) { server.Kill(); server.WaitForExit(2500); } } catch { }
                    }
                    WriteReceipt("stopped", exitCode);
                    server.Dispose(); server = null;
                }
                serverPid = 0; if (attachmentCleared) attachedBusId = null;
            }
        }
        private void NoteCleanupFailure(string code)
        {
            cleanupBlocked = true;
            if (cleanupCode == null) cleanupCode = code;
            RecordDiagnostic("cleanup_unconfirmed", code);
        }

        private void InitializeDiagnostics()
        {
            string directory = Path.Combine(baseDir, "diagnostics"); Directory.CreateDirectory(directory);
            sessionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            receiptPath = Path.Combine(directory, sessionId + "-session.json");
            stdoutPath = Path.Combine(directory, sessionId + "-stdout.jsonl"); stderrPath = Path.Combine(directory, sessionId + "-stderr.jsonl");
            Interlocked.Exchange(ref stdoutLines, 0); Interlocked.Exchange(ref stderrLines, 0); Interlocked.Exchange(ref featureWarnings, 0);
            Interlocked.Exchange(ref framesSent, 0); Interlocked.Exchange(ref nonNeutralFramesSent, 0);
            File.WriteAllText(stdoutPath, "", new UTF8Encoding(false)); File.WriteAllText(stderrPath, "", new UTF8Encoding(false));
        }
        internal static Dictionary<string, object> SanitizeServerLine(string channel, string line)
        {
            string text = line ?? "";
            Dictionary<string, object> record = new Dictionary<string, object>();
            record["utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); record["channel"] = channel;
            record["lineLength"] = text.Length;
            record["category"] = text.IndexOf("unhandled", StringComparison.OrdinalIgnoreCase) >= 0 ? "unhandled_request" :
                text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ? "error" :
                text.IndexOf("WARN", StringComparison.OrdinalIgnoreCase) >= 0 ? "warning" :
                text.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ? "disconnected" :
                text.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0 ? "connected" : "event";
            foreach (Match match in Regex.Matches(text, @"\b(reportType|reportID|wLength|dataLen|bmRequestType|bRequest|wIndex)=(0x[0-9a-fA-F]{1,8}|[0-9]{1,8})\b"))
                record[match.Groups[1].Value] = match.Groups[2].Value;
            return record;
        }
        private void RecordServerLine(string channel, string line)
        {
            long count = channel == "stdout" ? Interlocked.Increment(ref stdoutLines) : Interlocked.Increment(ref stderrLines);
            if (line.IndexOf("unhandled", StringComparison.OrdinalIgnoreCase) >= 0) Interlocked.Increment(ref featureWarnings);
            if (count > 10000) return; // Bound each session's local diagnostic volume.
            Dictionary<string, object> record = SanitizeServerLine(channel, line);
            try { lock (logGate) { string path = channel == "stdout" ? stdoutPath : stderrPath; if (path != null) File.AppendAllText(path, new JavaScriptSerializer().Serialize(record) + "\n", new UTF8Encoding(false)); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void RecordDiagnostic(string category, string exceptionType)
        {
            try { lock (logGate) { if (stderrPath != null) File.AppendAllText(stderrPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }, { "category", category }, { "exceptionType", exceptionType }
            }) + "\n", new UTF8Encoding(false)); } } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void WriteReceipt(string state, int? exitCode)
        {
            if (receiptPath == null) return;
            try { WriteAtomic(receiptPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "version", BuildInfo.Version }, { "sourceCommit", BuildInfo.SourceCommit }, { "workingTreeDirty", BuildInfo.WorkingTreeDirty }, { "builtUtc", BuildInfo.BuiltUtc }, { "controllerType", controllerType },
                { "session", sessionId }, { "ownerPid", ownPid }, { "serverPid", serverPid }, { "serverStartedUtc", serverStartedUtc.ToString("o", CultureInfo.InvariantCulture) },
                { "executable", Path.Combine(baseDir, "DualSenseCandidateServer.exe") }, { "apiPort", ApiPort }, { "usbPort", UsbPort }, { "busId", attachedBusId },
                { "attachedPort", attachedPort }, { "attachUnresolved", attachAttempted }, { "usbipCommandTerminationUnconfirmed", usbipCommandTerminationUnconfirmed },
                { "unconfirmedUsbipCommandPid", unconfirmedUsbipCommandPid }, { "state", state }, { "serverExitCode", exitCode }, { "failure", failureCode }, { "cleanup", cleanupCode }, { "cleanupBlocked", cleanupBlocked }, { "sonyAcceptanceVerified", false }
            })); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void StartStatusWriter()
        {
            if (statusPath == null) return;
            statusWorker = new Thread(new ThreadStart(delegate { do { TryWriteStatus(); } while (!statusStop.WaitOne(500)); TryWriteStatus(); })) { IsBackground = true, Name = "Controller-free metadata" }; statusWorker.Start();
        }
        private void TryWriteStatus()
        {
            CandidateTransport current = transport;
            try { WriteAtomic(statusPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "version", BuildInfo.Version }, { "sourceCommit", BuildInfo.SourceCommit }, { "workingTreeDirty", BuildInfo.WorkingTreeDirty }, { "builtUtc", BuildInfo.BuiltUtc }, { "controllerType", controllerType },
                { "kind", "dualsense-compatibility-preview" }, { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }, { "pid", ownPid }, { "serverPid", serverPid },
                { "enabled", enabled }, { "editing", editing }, { "usbAttachedVerified", usbAttachedVerified }, { "transportConnected", current != null && current.IsConnected },
                { "usbipPort", attachedPort }, { "busId", attachedBusId }, { "attachUnresolved", attachAttempted && !usbAttachedVerified }, { "framesSent", Interlocked.Read(ref framesSent) }, { "nonNeutralFramesSent", Interlocked.Read(ref nonNeutralFramesSent) },
                { "stdoutLines", Interlocked.Read(ref stdoutLines) }, { "stderrLines", Interlocked.Read(ref stderrLines) }, { "unhandledRequests", Interlocked.Read(ref featureWarnings) },
                { "failure", failureCode }, { "cleanup", cleanupCode }, { "usbipCommandTerminationUnconfirmed", usbipCommandTerminationUnconfirmed },
                { "unconfirmedUsbipCommandPid", unconfirmedUsbipCommandPid }, { "cleanupBlocked", cleanupBlocked }, { "sonyAcceptanceVerified", false }
            })); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private static void WriteAtomic(string destination, string json)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, json + "\n", new UTF8Encoding(false)); if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (preview) { base.OnFormClosing(e); return; }
            closing = true; enabled = false; input.Reset(); uiTimer.Stop();
            if (hotkeyRegistered) UnregisterHotKey(Handle, ToggleHotkey);
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            Cleanup(); statusStop.Set();
            if (statusWorker == null || statusWorker.Join(2500)) statusStop.Dispose();
            uiTimer.Dispose(); base.OnFormClosing(e);
        }
        private sealed class ExperimentException : Exception
        {
            internal readonly string Code;
            internal ExperimentException(string code) : base(code) { Code = code; }
        }
    }
}
