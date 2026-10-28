using System;
using System.Diagnostics;
using System.Drawing;
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
        private readonly KeyboardInputState input = new KeyboardInputState();
        private readonly TextBox bus = new TextBox();
        private readonly NumericUpDown endpoint = new NumericUpDown();
        private readonly ComboBox product = new ComboBox();
        private readonly Button start = new Button(), toggle = new Button(), stop = new Button();
        private readonly Label status = new Label(), counts = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly string sonyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Sony", "PS Remote Play", "RemotePlay.exe");
        private readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;
        private readonly OptionalStatusFile statusFile;
        private volatile UsbIpRelay relay;
        private volatile string startupFailure;
        private volatile bool enabled, closing;
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
            PhysicalKeyboardOptions options;
            try { options = PhysicalKeyboardOptions.Parse(args); }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "PS 鍵盤");
                return 2;
            }
            bool created;
            using (Mutex instance = new Mutex(true, "Local\\PSKeyboardPhysicalRelay", out created))
            {
                if (!created) { MessageBox.Show("實體手把中繼程式已開啟。", "PS 鍵盤"); return 1; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new PhysicalKeyboard(options.BusId, options.StartRelay, options.StatusFile));
            }
            return 0;
        }

        private PhysicalKeyboard(string busId, bool autoStartRelay, string statusPath)
        {
            statusFile = new OptionalStatusFile(statusPath, GetStatusSnapshot);
            Text = "PS 鍵盤 — 實體手把中繼測試版";
            ClientSize = new Size(700, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            Font = new Font("Microsoft JhengHei UI", 11);
            BackColor = Color.FromArgb(245, 247, 251);
            Label title = new Label { Text = "鍵盤＋實體 PS4 手把", AutoSize = true,
                Font = new Font(Font.FontFamily, 21, FontStyle.Bold), Location = new Point(24, 18) };
            Label note = new Label { Text = "保留實體手把通訊 · 鍵盤專用 · 官方 Remote Play 尚未驗收",
                AutoSize = true, Location = new Point(26, 62) };
            Label busLabel = new Label { Text = "手把連接位置", AutoSize = true, Location = new Point(26, 105) };
            bus.Text = busId; bus.SetBounds(148, 101, 105, 28); bus.MaxLength = 31;
            Label modelLabel = new Label { Text = "型號", AutoSize = true, Location = new Point(275, 105) };
            product.DropDownStyle = ComboBoxStyle.DropDownList;
            product.Items.AddRange(new object[] { "PS4 第一代（05C4）", "PS4 第二代（09CC）" });
            product.SelectedIndex = 0; product.SetBounds(320, 101, 215, 28);
            Label epLabel = new Label { Text = "資料通道", AutoSize = true, Location = new Point(26, 145) };
            endpoint.Minimum = 1; endpoint.Maximum = 15; endpoint.Value = 4;
            endpoint.SetBounds(148, 141, 65, 28);
            Label configNote = new Label { Text = "依實體手把檢查結果設定，連線期間不可修改。",
                AutoSize = true, Location = new Point(235, 145), Font = new Font(Font.FontFamily, 9) };
            start.Text = "啟動中繼"; start.SetBounds(26, 188, 150, 40); start.Click += delegate { StartRelay(); };
            toggle.Text = "啟用鍵盤（F8）"; toggle.SetBounds(190, 188, 210, 40); toggle.Enabled = false;
            toggle.Click += delegate { Toggle(); };
            stop.Text = "停止中繼"; stop.SetBounds(414, 188, 150, 40); stop.Enabled = false;
            stop.Click += delegate { StopRelay(); status.Text = "中繼已停止，鍵盤已釋放。請依還原步驟確認實體手把重新接回。"; };
            status.Text = "尚未接通實體手把。中繼需完成實體手把接入後才能啟用鍵盤。";
            status.SetBounds(26, 246, 645, 58);
            counts.Text = "已收到手把資料：0　已合併鍵盤：0"; counts.SetBounds(26, 306, 645, 28);
            Label keys = new Label { Text = "W A S D　左搖桿          方向鍵　十字鍵\r\nI J L K　△ □ ○ ×         Q E　L1 R1\r\nU O　L2 R2                1 2　L3 R3\r\nEnter　Options            Backspace　Share\r\nT　觸控板按下             P　PS 鍵",
                Location = new Point(26, 348), Size = new Size(640, 126) };
            Label bottom = new Label { Text = "F8 暫停鍵盤；停止中繼會中斷手把連線，需依還原步驟確認接回。",
                Location = new Point(26, 488), AutoSize = true, Font = new Font(Font.FontFamily, 9) };
            Controls.AddRange(new Control[] { title, note, busLabel, bus, modelLabel, product,
                epLabel, endpoint, configNote, start, toggle, stop, status, counts, keys, bottom });
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

        private void InstallKeyboardHook()
        {
            hookCallback = KeyboardHook;
            hook = SetWindowsHookEx(13, hookCallback, GetModuleHandle(null), 0);
            hotkeyRegistered = hook != IntPtr.Zero && RegisterHotKey(Handle, ToggleHotkey, 0x4000, 0x77);
            if (!hotkeyRegistered)
            {
                start.Enabled = false;
                startupFailure = "Keyboard input setup failed";
                status.Text = "無法設定鍵盤或 F8 暫停鍵，尚未接管輸入。";
                if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
                return;
            }
            for (int key = 0; key < 256; key++)
                if (KeyboardMapping.ActionForKey(key) != 0 && (GetAsyncKeyState(key) & 0x8000) != 0)
                    input.Process(key, true, false, false, false);
        }

        private void UpdateFocus()
        {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            long now = Stopwatch.GetTimestamp();
            if (pid == lastFocusPid && now - lastFocusCheck < Stopwatch.Frequency / 4) return;
            lastFocusPid = pid; lastFocusCheck = now;
            bool allowed = pid == ownPid;
            if (!allowed && pid != 0)
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
            return pid == ownPid || (decision.Pid == pid && decision.Allowed);
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
                bool capture = enabled && !closing && current != null && current.IsAttached && current.EndpointVerified &&
                    !modifiers && HookForeground();
                if (input.Process((int)key.Key, down, up, (key.Flags & 0x10) != 0, capture))
                    return new IntPtr(1);
            }
            return CallNextHookEx(hook, code, message, data);
        }

        private byte[] MergePhysicalReport(byte[] physical)
        {
            // A pause may race one callback already in progress. Every following
            // callback takes a fresh enabled state and a fresh physical report;
            // no cached merged report is ever repeated after pause or release.
            bool active = enabled && !closing && !ModifiersDown() && HookForeground();
            if (!active) input.Reset();
            return PhysicalReportMerge.Merge(physical, active ? input.Held : 0, active);
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
            if (closing || relay != null || !hotkeyRegistered) return;
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
                status.Text = "中繼已啟動，尚未接通實體手把。鍵盤維持暫停。";
            }
            catch (Exception ex)
            {
                startupFailure = "Relay startup failed";
                if (candidate != null) candidate.Dispose();
                status.Text = "無法啟動中繼：" + ex.Message;
            }
        }

        private void SetEnabled(bool value)
        {
            enabled = false;
            input.Reset();
            if (value && !closing) enabled = true;
            toggle.Text = enabled ? "暫停鍵盤（F8）" : "啟用鍵盤（F8）";
        }

        private void Toggle()
        {
            UsbIpRelay current = relay;
            if (closing || !toggle.Enabled || current == null || !current.IsAttached || !current.EndpointVerified) return;
            SetEnabled(!enabled);
            RefreshState();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x312 && message.WParam.ToInt32() == ToggleHotkey) { Toggle(); return; }
            base.WndProc(ref message);
        }

        private void RefreshState()
        {
            if (closing) return;
            UpdateFocus();
            if (!HookForeground() || ModifiersDown()) input.Reset();
            UsbIpRelay current = relay;
            if (current == null) return;
            long reports = current.InputReports;
            counts.Text = "已收到手把資料：" + reports + "　已合併鍵盤：" + current.MergedReports;
            long now = Stopwatch.GetTimestamp();
            if (reports != lastReportCount) { lastReportCount = reports; lastReportAt = now; }
            if (current.Failure != null)
            {
                SetEnabled(false); toggle.Enabled = false;
                status.Text = "中繼已中止：" + current.Failure + "\r\n請先停止中繼，再檢查實體手把接入狀態。";
                return;
            }
            if (!current.IsAttached)
            {
                SetEnabled(false); toggle.Enabled = false;
                status.Text = hadAttachment ? "實體手把通道已中斷，鍵盤已暫停。請停止中繼後重試。" :
                    "中繼已啟動，尚未接通實體手把。鍵盤維持暫停。";
                return;
            }
            hadAttachment = true;
            if (!current.EndpointVerified)
            {
                SetEnabled(false); toggle.Enabled = false;
                status.Text = "手把通道已接通，仍在確認實體手把的按鍵資料通道。鍵盤維持暫停。";
                return;
            }
            bool fresh = reports > 0 && now - lastReportAt < Stopwatch.Frequency * 2;
            toggle.Enabled = fresh;
            if (!fresh)
            {
                SetEnabled(false);
                status.Text = "手把通道已接通，仍在等待有效的按鍵資料。鍵盤維持暫停。";
            }
            else if (!enabled) status.Text = "已收到實體手把按鍵資料。按 F8 啟用鍵盤；PS5 操作仍待實測。";
            else if (!HookForeground()) status.Text = "已暫停鍵盤輸出；切回本視窗或官方 Remote Play 即可操作。";
            else status.Text = "正在合併鍵盤與實體手把輸入。F8 暫停；PS5 操作仍待實測。";
        }

        private void StopRelay()
        {
            SetEnabled(false); toggle.Enabled = false;
            UsbIpRelay old = relay; relay = null;
            if (old != null) old.Dispose();
            stop.Enabled = false; start.Enabled = hotkeyRegistered;
            bus.Enabled = product.Enabled = endpoint.Enabled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            closing = true; enabled = false; input.Reset(); timer.Stop();
            if (hotkeyRegistered) UnregisterHotKey(Handle, ToggleHotkey);
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            StopRelay();
            statusFile.Dispose();
            timer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
