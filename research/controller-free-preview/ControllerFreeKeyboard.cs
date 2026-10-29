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
    internal sealed class ControllerFreeKeyboard : Form
    {
        private const int ApiPort = 33242, UsbPort = 33241, ToggleHotkey = 0x504D;
        private const string BackendHash = "142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6";
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
        private string controllerType;
        private readonly Label status = new Label(), keyboardStatus = new Label(), notice = new Label();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly ManualResetEvent statusStop = new ManualResetEvent(false);
        private Thread sender, statusWorker;
        private volatile ViiperTransport transport;
        private Process server;
        private volatile int serverPid, attachedPort, unconfirmedUsbipCommandPid;
        private volatile string attachedBusId, failureCode, cleanupCode;
        private volatile bool enabled, closing, editing, busy, usbAttachedVerified, attachAttempted, usbipCommandTerminationUnconfirmed;
        private long framesSent, nonNeutralFramesSent, stdoutLines, stderrLines, featureWarnings;
        private string receiptPath, stdoutPath, stderrPath, sessionId;
        private DateTime serverStartedUtc;
        private IntPtr hook;
        private HookCallback callback;
        private bool hotkeyRegistered;
        private BindingEditor activeEditor;
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
            bool autoConnect; string statusFile, deviceType;
            try { ParseOptions(args, out autoConnect, out statusFile, out deviceType); }
            catch (ArgumentException)
            {
                MessageBox.Show("用法：PSKeyboard-ControllerFree.exe [--connect] [--controller dualshock4|dualsense] [--status-file 本機完整 JSON 路徑]", "上班不要玩 " + BuildInfo.Version); return 2;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\PSKeyboardControllerFreeExperiment", out created))
            {
                if (!created) { MessageBox.Show("免手把實驗版已經開啟。", "上班不要玩"); return 1; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ControllerFreeKeyboard(autoConnect, statusFile, deviceType));
            }
            return 0;
        }

        internal static void ParseOptions(string[] args, out bool autoConnect, out string statusFile)
        {
            string ignored; ParseOptions(args, out autoConnect, out statusFile, out ignored);
        }
        internal static void ParseOptions(string[] args, out bool autoConnect, out string statusFile, out string deviceType)
        {
            autoConnect = false; statusFile = null; deviceType = "dualshock4"; bool typeSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--connect" && !autoConnect) autoConnect = true;
                else if (args[i] == "--status-file" && statusFile == null && i + 1 < args.Length) statusFile = ValidateStatusPath(args[++i]);
                else if (args[i] == "--controller" && !typeSet && i + 1 < args.Length)
                {
                    deviceType = args[++i];
                    if (deviceType != "dualshock4" && deviceType != "dualsense") throw new ArgumentException("Unknown controller type.");
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
            if (!Directory.Exists(Path.GetDirectoryName(result))) throw new ArgumentException("Status directory must already exist.");
            return result;
        }

        private ControllerFreeKeyboard(bool autoConnect, string statusFile, string deviceType)
        {
            statusPath = statusFile; controllerType = deviceType;
            profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSKeyboard", "bindings.json");
            profile = BindingProfile.Default(); bool badProfile = false;
            try { profile = BindingProfile.Load(profilePath); } catch { badProfile = true; }
            input = new KeyboardControlPipeline(delegate { return profile; });
            Text = "上班不要玩 " + BuildInfo.Version + " — 免手把測試版"; ClientSize = new Size(820, 530); MinimumSize = new Size(820, 570);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = KeyboardUi.Background; ForeColor = KeyboardUi.Ink; Font = KeyboardUi.Font(10, FontStyle.Regular);
            TableLayoutPanel root = KeyboardUi.Table(1); root.Padding = new Padding(28); root.RowCount = 8;
            foreach (int height in new int[] { 48, 60, 46, 60, 74, 46, 62, 52 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Controls.Add(root);
            Label title = KeyboardUi.Label("上班不要玩 · " + BuildInfo.Version, 23, KeyboardUi.Ink); title.Font = KeyboardUi.Font(23, FontStyle.Bold); title.Dock = DockStyle.Fill;
            root.Controls.Add(title, 0, 0);
            Label description = KeyboardUi.Label("免實體手把，使用鍵盤控制官方 Remote Play。\r\nPS4 模式已實測可用；PS5 模式尚未通過 Remote Play 操作測試。", 10, KeyboardUi.Amber); description.Dock = DockStyle.Fill;
            root.Controls.Add(description, 0, 1);
            FlowLayoutPanel modelRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
            Label modelLabel = KeyboardUi.Label("模擬手把", 10, KeyboardUi.Ink); modelLabel.Size = new Size(95, 32);
            controllerChoice.DropDownStyle = ComboBoxStyle.DropDownList; controllerChoice.Width = 285;
            controllerChoice.Items.AddRange(new object[] { "PS4 · DUALSHOCK 4（目前可用）", "PS5 · DualSense（實驗模式）" });
            controllerChoice.SelectedIndex = deviceType == "dualsense" ? 1 : 0;
            controllerChoice.SelectedIndexChanged += delegate { controllerType = controllerChoice.SelectedIndex == 1 ? "dualsense" : "dualshock4"; };
            modelRow.Controls.Add(modelLabel); modelRow.Controls.Add(controllerChoice); root.Controls.Add(modelRow, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
            KeyboardUi.Button(connect, "建立實驗手把", false); KeyboardUi.Button(toggle, "啟用鍵盤 · F8", true); KeyboardUi.Button(configure, "按鍵與連點設定", false);
            toggle.Enabled = false; connect.Click += ConnectClick; toggle.Click += delegate { Toggle(); }; configure.Click += delegate { EditBindings(); };
            actions.Controls.AddRange(new Control[] { connect, toggle, configure }); root.Controls.Add(actions, 0, 3);
            status.Text = "尚未建立虛擬手把。"; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.BackColor = Color.White; status.Padding = new Padding(14);
            root.Controls.Add(status, 0, 4);
            keyboardStatus.Dock = DockStyle.Fill; keyboardStatus.TextAlign = ContentAlignment.MiddleLeft; keyboardStatus.Text = "鍵盤已暫停。"; root.Controls.Add(keyboardStatus, 0, 5);
            Label help = KeyboardUi.Label("只接受官方 Remote Play 前景視窗中的按鍵；切換視窗會停止全部連點。\r\n回到 Remote Play 後需重新按鍵。F8 隨時暫停；不會關閉或重開 Sony 程式。", 10, KeyboardUi.Muted); help.Dock = DockStyle.Fill;
            root.Controls.Add(help, 0, 6);
            notice.Text = badProfile ? "原設定檔讀取失敗，目前使用預設；尚未覆寫檔案。" : "共用原有按鍵設定；本版為測試版，尚未正式發布。";
            notice.ForeColor = badProfile ? KeyboardUi.Amber : KeyboardUi.Muted; notice.Dock = DockStyle.Fill; notice.TextAlign = ContentAlignment.MiddleLeft; root.Controls.Add(notice, 0, 7);
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
                    if (autoConnect) BeginInvoke(new Action(delegate { ConnectClick(null, EventArgs.Empty); }));
                }
                StartStatusWriter();
            };
            uiTimer.Interval = 100; uiTimer.Tick += delegate { RefreshStatus(); }; uiTimer.Start();
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
                ViiperTransport current = transport;
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
            ViiperTransport current = transport;
            if (closing || editing || !toggle.Enabled || !usbAttachedVerified || current == null || !current.IsConnected) return;
            SetEnabled(!enabled); RefreshStatus();
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x312 && message.WParam.ToInt32() == ToggleHotkey)
            {
                if (editing && activeEditor != null) activeEditor.ReservedHotkeyPressed(); else Toggle(); return;
            }
            base.WndProc(ref message);
        }
        private void EditBindings()
        {
            if (closing || editing || busy) return;
            editing = true; SetEnabled(false);
            try
            {
                using (BindingEditor editor = new BindingEditor(profile))
                {
                    activeEditor = editor;
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    try { editor.Result.Save(profilePath); }
                    catch { MessageBox.Show(this, UiText.T("ProfileSaveFailure"), UiText.T("BindingsTitle")); return; }
                    profile = editor.Result; input.Reset(); notice.Text = "設定已儲存；切回 Remote Play，再按 F8 啟用。";
                }
            }
            finally { activeEditor = null; SetEnabled(false); editing = false; RefreshStatus(); }
        }
        private void RefreshStatus()
        {
            if (closing) return;
            UpdateFocus(); bool foreground = SonyForeground();
            if (!foreground || ModifiersDown() || editing) input.Reset();
            ViiperTransport current = transport;
            if (current != null && !current.IsConnected) { enabled = false; input.Reset(); toggle.Enabled = false; }
            if (failureCode != null)
            {
                SetEnabled(false); toggle.Enabled = false;
                status.Text = FailureMessage(failureCode) + (cleanupCode == null ? "" : "\r\nUSB 移除狀態未完全確認，詳見本機診斷。");
            }
            keyboardStatus.Text = !enabled ? "鍵盤已暫停。" : !foreground || ModifiersDown() ? "等待 Remote Play 前景視窗；連點已清除。" : "鍵盤映射已啟用；F8 暫停。PS5 是否收到仍需實測。";
        }
        private static string FailureMessage(string code)
        {
            switch (code)
            {
                case "keyboard_setup_failed": return "F8 或鍵盤擷取無法啟用，請先關閉其他版本的鍵盤程式。";
                case "usbip_missing": return "找不到已安裝的 USBip；本程式不會自動安裝驅動。";
                case "backend_invalid": return "VIIPER 元件遺失或驗證失敗。";
                case "ports_unavailable": return "實驗通道正由其他程式使用，沒有啟動新手把。";
                case "usb_attach_failed": return "USB 手把接入未能確認；尚未啟用鍵盤。";
                case "send_failed": return "虛擬手把輸出通道已中斷，鍵盤已暫停。";
                case "cancelled": return "操作已取消。";
                default: return "建立實驗手把失敗。請查看本機診斷狀態。";
            }
        }

        private void UpdateUi(Action action)
        { if (!closing && IsHandleCreated) try { BeginInvoke(action); } catch (InvalidOperationException) { } }
        private void ConnectClick(object senderObject, EventArgs args)
        {
            if (closing || editing || busy || !hotkeyRegistered) return;
            if (usbipCommandTerminationUnconfirmed || (attachAttempted && server == null && transport == null))
            { status.Text = "上次 USB 移除尚未確認，暫不建立新裝置；請先核對本機診斷中的接入位置。"; return; }
            if (transport != null || server != null)
            {
                if (MessageBox.Show(this, "移除這隻實驗手把會中斷它的輸入。確定要移除？", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                SetEnabled(false); busy = true; connect.Enabled = toggle.Enabled = configure.Enabled = false;
                ThreadPool.QueueUserWorkItem(delegate { Cleanup(); UpdateUi(delegate { busy = false; connect.Enabled = configure.Enabled = true; controllerChoice.Enabled = !attachAttempted && !usbipCommandTerminationUnconfirmed; connect.Text = "建立實驗手把"; status.Text = cleanupCode == null ? "實驗手把已移除，鍵盤已暫停。" : "輸出已停止，但 USB 移除狀態未完全確認。"; }); });
                return;
            }
            busy = true; connect.Enabled = configure.Enabled = controllerChoice.Enabled = false; failureCode = cleanupCode = null; SetEnabled(false); status.Text = "正在建立 " + (controllerType == "dualsense" ? "PS5 DualSense" : "PS4 DUALSHOCK 4") + " 實驗手把…";
            ThreadPool.QueueUserWorkItem(delegate {
                lock (sessionGate)
                {
                    try
                    {
                        CheckClosing();
                        if (!File.Exists(usbipPath)) throw new ExperimentException("usbip_missing");
                        string executable = Path.Combine(baseDir, "viiper.exe");
                        if (!ValidBackend(executable)) throw new ExperimentException("backend_invalid");
                        if (ListenerPid(ApiPort) != 0 || ListenerPid(UsbPort) != 0) throw new ExperimentException("ports_unavailable");
                        InitializeDiagnostics();
                        ProcessStartInfo launch = new ProcessStartInfo(executable, "server --api.addr=127.0.0.1:" + ApiPort + " --usb.addr=127.0.0.1:" + UsbPort + " --api.auto-attach-local-client=false --no-tray --update-notify=none") {
                            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                            WorkingDirectory = baseDir, RedirectStandardOutput = true, RedirectStandardError = true };
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
                        ViiperTransport candidate = new ViiperTransport(ApiPort, controllerType); transport = candidate; candidate.Connect();
                        if (candidate.ServerVersion != "0.9.0") throw new ExperimentException("backend_invalid");
                        CheckClosing(); attachedBusId = candidate.UsbBusId;
                        attachAttempted = true; WriteReceipt("attach_requested", null);
                        string output = Usbip("--tcp-port " + UsbPort + " attach --remote 127.0.0.1 --bus-id " + attachedBusId + " --once --terse");
                        int parsed;
                        if (!Int32.TryParse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || parsed <= 0) throw new ExperimentException("usb_attach_failed");
                        attachedPort = parsed;
                        if (!OwnPortUrl(Usbip("port " + parsed), attachedBusId)) throw new ExperimentException("usb_attach_failed");
                        usbAttachedVerified = true; WriteReceipt("attached", null); CheckClosing();
                        sender = new Thread(SendLoop) { IsBackground = true, Name = "Controller-free state sender" }; sender.Start();
                        UpdateUi(delegate { busy = false; connect.Enabled = configure.Enabled = toggle.Enabled = true; connect.Text = "移除實驗手把"; status.Text = "已核對 USBip 接入位置。按 F8 啟用鍵盤。\r\n這不代表官方 Remote Play 已接受這隻手把。"; });
                    }
                    catch (Exception ex)
                    {
                        ExperimentException expected = ex as ExperimentException;
                        failureCode = expected == null ? "connect_failed" : expected.Code;
                        RecordDiagnostic("connect_failure", ex.GetType().Name); Cleanup();
                        UpdateUi(delegate { busy = false; connect.Enabled = configure.Enabled = true; controllerChoice.Enabled = !attachAttempted && !usbipCommandTerminationUnconfirmed; connect.Text = "建立實驗手把"; RefreshStatus(); });
                    }
                }
            });
        }
        private void CheckClosing() { if (closing) throw new ExperimentException("cancelled"); }
        private static bool ValidBackend(string path)
        {
            if (!File.Exists(path)) return false;
            using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "") == BackendHash;
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
            cleanupCode = "usbip_command_stop_unconfirmed";
        }
        private void SendLoop()
        {
            ViiperTransport session = transport;
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
                if (session != null && session.IsConnected) try { session.Send(session.NeutralFrame()); } catch { cleanupCode = "neutral_unconfirmed"; }
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
                ViiperTransport old = transport; transport = null;
                if (sender != null && sender != Thread.CurrentThread) sender.Join(2500); sender = null;
                if (old != null && old.IsConnected) try { old.Send(ViiperTransport.CreateNeutralFrame()); } catch { cleanupCode = "neutral_unconfirmed"; }
                bool attachmentCleared = !attachAttempted && !usbipCommandTerminationUnconfirmed;
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
                        if (!attachmentCleared) cleanupCode = "detach_unconfirmed";
                    }
                    catch { cleanupCode = "detach_unconfirmed"; }
                }
                if (usbipCommandTerminationUnconfirmed) { attachmentCleared = false; cleanupCode = "usbip_command_stop_unconfirmed"; }
                if (attachmentCleared) { attachedPort = 0; attachAttempted = false; }
                if (old != null) try { old.Dispose(); } catch { cleanupCode = "bus_cleanup_unconfirmed"; }
                int? exitCode = null;
                if (server != null)
                {
                    try { if (!server.HasExited) server.Kill(); if (server.WaitForExit(2500)) exitCode = server.ExitCode; else cleanupCode = "server_stop_unconfirmed"; }
                    catch { cleanupCode = "server_stop_unconfirmed"; }
                    WriteReceipt("stopped", exitCode);
                    server.Dispose(); server = null;
                }
                serverPid = 0; if (attachmentCleared) attachedBusId = null;
            }
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
                { "executable", Path.Combine(baseDir, "viiper.exe") }, { "apiPort", ApiPort }, { "usbPort", UsbPort }, { "busId", attachedBusId },
                { "attachedPort", attachedPort }, { "attachUnresolved", attachAttempted }, { "usbipCommandTerminationUnconfirmed", usbipCommandTerminationUnconfirmed },
                { "unconfirmedUsbipCommandPid", unconfirmedUsbipCommandPid }, { "state", state }, { "serverExitCode", exitCode }, { "failure", failureCode }, { "cleanup", cleanupCode }
            })); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void StartStatusWriter()
        {
            if (statusPath == null) return;
            statusWorker = new Thread(new ThreadStart(delegate { do { TryWriteStatus(); } while (!statusStop.WaitOne(500)); TryWriteStatus(); })) { IsBackground = true, Name = "Controller-free metadata" }; statusWorker.Start();
        }
        private void TryWriteStatus()
        {
            ViiperTransport current = transport;
            try { WriteAtomic(statusPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                { "version", BuildInfo.Version }, { "sourceCommit", BuildInfo.SourceCommit }, { "workingTreeDirty", BuildInfo.WorkingTreeDirty }, { "builtUtc", BuildInfo.BuiltUtc }, { "controllerType", controllerType },
                { "kind", "controller-free-experiment" }, { "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }, { "pid", ownPid }, { "serverPid", serverPid },
                { "enabled", enabled }, { "editing", editing }, { "usbAttachedVerified", usbAttachedVerified }, { "transportConnected", current != null && current.IsConnected },
                { "usbipPort", attachedPort }, { "busId", attachedBusId }, { "attachUnresolved", attachAttempted && !usbAttachedVerified }, { "framesSent", Interlocked.Read(ref framesSent) }, { "nonNeutralFramesSent", Interlocked.Read(ref nonNeutralFramesSent) },
                { "stdoutLines", Interlocked.Read(ref stdoutLines) }, { "stderrLines", Interlocked.Read(ref stderrLines) }, { "unhandledRequests", Interlocked.Read(ref featureWarnings) },
                { "failure", failureCode }, { "cleanup", cleanupCode }, { "usbipCommandTerminationUnconfirmed", usbipCommandTerminationUnconfirmed },
                { "unconfirmedUsbipCommandPid", unconfirmedUsbipCommandPid }, { "sonyAcceptanceVerified", false }
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
            if (!closing && e.CloseReason == CloseReason.UserClosing && (transport != null || server != null) &&
                MessageBox.Show(this, "關閉程式會停止並移除這隻實驗手把。確定關閉？", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { e.Cancel = true; base.OnFormClosing(e); return; }
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
