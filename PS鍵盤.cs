using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ViGEm's DS4 report layout and button values are from its MIT-licensed Common.h.
// Copyright (c) 2017-2023 Nefarius Software Solutions e.U. and contributors.
namespace PSKeyboard {
    [StructLayout(LayoutKind.Sequential)]
    struct Report { public byte LX, LY, RX, RY; public ushort Buttons; public byte Special, L2, R2; }
    static class Native {
        const string Dll = "ViGEmClient.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vigem_alloc();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_free(IntPtr client);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_connect(IntPtr client);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_disconnect(IntPtr client);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vigem_target_ds4_alloc();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_target_free(IntPtr target);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_target_add(IntPtr client, IntPtr target);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_target_remove(IntPtr client, IntPtr target);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_target_ds4_update(IntPtr client, IntPtr target, Report report);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [StructLayout(LayoutKind.Sequential)] public struct RawDevice { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool ClipCursor(ref Rect rectangle);
        [DllImport("user32.dll", EntryPoint = "ClipCursor")] public static extern bool ReleaseCursor(IntPtr rectangle);
    }
    sealed class MainWindow : Form {
        const uint Success = 0x20000000;
        IntPtr client, target; bool connected, added, active, previousF8, neutralSent, cursorCaptured;
        int mouseX, mouseY; bool rawAvailable;
        readonly Label status = new Label(); readonly Button connectButton = new Button(); readonly Button activeButton = new Button();
        readonly CheckBox mouseControl = new CheckBox(); readonly TrackBar sensitivity = new TrackBar();
        readonly Timer timer = new Timer();
        public MainWindow() {
            Text = "PS 鍵盤控制器（測試版）"; ClientSize = new Size(680, 615); StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft JhengHei UI", 10); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            Label title = new Label { Text = "PS Remote Play 鍵盤控制器", Font = new Font(Font.FontFamily, 17, FontStyle.Bold), Location = new Point(22, 18), AutoSize = true };
            Label note = new Label { Text = "此原型模擬 DualShock 4。官方 Remote Play 目前版本相容性尚未驗證，可能無法使用。\n需要安裝 ViGEmBus；不會自動安裝。僅在 PS Remote Play 為前景視窗時送出輸入。", Location = new Point(25, 58), Size = new Size(630, 62) };
            connectButton.Text = "連接虛擬手把"; connectButton.Location = new Point(25, 130); connectButton.Size = new Size(170, 38); connectButton.Click += delegate { Connect(); };
            activeButton.Text = "啟用控制（F8）"; activeButton.Location = new Point(210, 130); activeButton.Size = new Size(170, 38); activeButton.Enabled = false; activeButton.Click += delegate { Toggle(); };
            mouseControl.Text = "滑鼠控制右搖桿"; mouseControl.Location = new Point(25, 226); mouseControl.Size = new Size(165, 30); mouseControl.Checked = true;
            mouseControl.CheckedChanged += delegate { if (!mouseControl.Checked) { mouseX = mouseY = 0; ReleaseMouse(); } };
            Label sensitivityLabel = new Label { Text = "滑鼠靈敏度", Location = new Point(205, 232), AutoSize = true };
            sensitivity.Minimum = 1; sensitivity.Maximum = 10; sensitivity.Value = 5; sensitivity.TickFrequency = 1; sensitivity.Location = new Point(305, 220); sensitivity.Size = new Size(190, 40);
            status.Location = new Point(25, 181); status.Size = new Size(620, 48); status.Text = "狀態：尚未連接。";
            Label mapping = new Label { Location = new Point(25, 272), Size = new Size(625, 290), Text = "預設按鍵\n\n左搖桿：W / A / S / D       右搖桿：滑鼠移動或方向鍵\n△：I       □：J       ○：L       ×：K\nL1：Q       R1：E       L2：U       R2：O\n方向鍵：數字鍵盤 8 / 4 / 2 / 6（請開啟 Num Lock）\nL3：1       R3：2       Options：Enter       Share：Backspace\n觸控板按下：T       PS：P\n\n啟用後切到 Remote Play；滑鼠會隱藏並限制在視窗內。\nF8 可全域暫停或繼續；暫停、失焦或關閉會釋放游標並使手把回中。" };
            Controls.AddRange(new Control[] { title, note, connectButton, activeButton, status, mouseControl, sensitivityLabel, sensitivity, mapping });
            timer.Interval = 16; timer.Tick += OnTick; timer.Start(); FormClosing += delegate { CloseDevice(); };
            Application.ApplicationExit += delegate { ReleaseMouse(); };
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            Native.RawDevice[] devices = { new Native.RawDevice { UsagePage = 1, Usage = 2, Flags = 0x100, Target = Handle } };
            rawAvailable = Native.RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf(typeof(Native.RawDevice)));
            if (!rawAvailable) { mouseControl.Checked = false; mouseControl.Enabled = false; status.Text = "無法啟用滑鼠輸入，仍可使用鍵盤。"; }
        }
        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x00FF && rawAvailable && cursorCaptured && active && mouseControl.Checked && IsRemotePlayForeground()) {
                uint size = 0; uint header = (uint)(IntPtr.Size == 8 ? 24 : 16);
                if (Native.GetRawInputData(m.LParam, 0x10000003, IntPtr.Zero, ref size, header) == 0 && size >= header + 20 && size <= 4096) {
                    IntPtr data = Marshal.AllocHGlobal((int)size);
                    try {
                        uint actual = size;
                        if (Native.GetRawInputData(m.LParam, 0x10000003, data, ref actual, header) == size && Marshal.ReadInt32(data, 0) == 0 && (Marshal.ReadInt16(data, (int)header) & 1) == 0) {
                            mouseX = Math.Max(-1000, Math.Min(1000, mouseX + Marshal.ReadInt32(data, (int)header + 12)));
                            mouseY = Math.Max(-1000, Math.Min(1000, mouseY + Marshal.ReadInt32(data, (int)header + 16)));
                        }
                    } finally { Marshal.FreeHGlobal(data); }
                }
            }
            base.WndProc(ref m);
        }
        void CaptureMouse() {
            Native.Rect rect; IntPtr foreground = Native.GetForegroundWindow();
            if (!Native.GetWindowRect(foreground, out rect) || !Native.ClipCursor(ref rect)) throw new InvalidOperationException("無法限制滑鼠游標，控制已停止。");
            if (!cursorCaptured) { Cursor.Hide(); cursorCaptured = true; mouseX = mouseY = 0; }
        }
        void ReleaseMouse() {
            if (cursorCaptured) { Native.ReleaseCursor(IntPtr.Zero); Cursor.Show(); cursorCaptured = false; }
            mouseX = mouseY = 0;
        }
        static bool Down(Keys k) { return (Native.GetAsyncKeyState((int)k) & 0x8000) != 0; }
        static Report Neutral() { return new Report { LX = 128, LY = 128, RX = 128, RY = 128, Buttons = 8 }; }
        static byte Axis(bool minus, bool plus) { return minus == plus ? (byte)128 : (minus ? (byte)0 : (byte)255); }
        static void Check(uint code, string operation) { if (code != Success) throw new InvalidOperationException(operation + " 失敗（0x" + code.ToString("X8") + "）。請確認 ViGEmBus 已安裝且可用。"); }
        void Connect() {
            if (connected) { active = false; CloseDevice(); status.Text = "狀態：已中斷連接。"; return; }
            try {
                client = Native.vigem_alloc(); if (client == IntPtr.Zero) throw new InvalidOperationException("無法建立 ViGEm 用戶端。");
                Check(Native.vigem_connect(client), "連接 ViGEmBus"); connected = true;
                target = Native.vigem_target_ds4_alloc(); if (target == IntPtr.Zero) throw new InvalidOperationException("無法建立虛擬手把。");
                Check(Native.vigem_target_add(client, target), "新增虛擬手把"); added = true;
                Check(Native.vigem_target_ds4_update(client, target, Neutral()), "初始化手把"); neutralSent = true;
                connectButton.Text = "中斷連接"; activeButton.Enabled = true; status.Text = "狀態：手把已建立，請按啟用後切換到 PS Remote Play。";
            } catch (Exception ex) {
                CloseDevice(); status.Text = "狀態：連接失敗。";
                MessageBox.Show("無法建立虛擬手把：\n" + ex.Message + "\n\n請確認 ViGEmClient.dll 與程式位於同一資料夾，且已安裝 ViGEmBus。", "連接失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        void Toggle() {
            if (!added) return; active = !active; neutralSent = false;
            activeButton.Text = active ? "暫停控制（F8）" : "啟用控制（F8）";
            status.Text = active ? "狀態：已啟用；等待 PS Remote Play 成為前景視窗。" : "狀態：已暫停，手把回中。";
            if (!active) {
                ReleaseMouse();
                try { SendNeutral(); }
                catch (Exception ex) { CloseDevice(); status.Text = "狀態：控制已停止。"; MessageBox.Show(ex.Message, "手把錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        bool IsRemotePlayForeground() {
            uint pid; IntPtr window = Native.GetForegroundWindow(); if (window == IntPtr.Zero) return false;
            Native.GetWindowThreadProcessId(window, out pid); if (pid == 0) return false;
            try { using (Process p = Process.GetProcessById((int)pid)) return String.Equals(p.ProcessName, "RemotePlay", StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; } catch (InvalidOperationException) { return false; }
        }
        void SendNeutral() {
            if (added && !neutralSent) { Check(Native.vigem_target_ds4_update(client, target, Neutral()), "重設手把"); neutralSent = true; }
        }
        void OnTick(object sender, EventArgs e) {
            bool f8 = Down(Keys.F8); if (f8 && !previousF8) Toggle(); previousF8 = f8;
            if (!added) return;
            try {
                if (!active || !IsRemotePlayForeground()) { ReleaseMouse(); SendNeutral(); if (active) status.Text = "狀態：已啟用；等待 PS Remote Play 成為前景視窗。"; return; }
                if (mouseControl.Checked && rawAvailable) CaptureMouse(); else ReleaseMouse();
                Report r = Neutral(); r.LX = Axis(Down(Keys.A), Down(Keys.D)); r.LY = Axis(Down(Keys.W), Down(Keys.S));
                r.RX = Axis(Down(Keys.Left), Down(Keys.Right)); r.RY = Axis(Down(Keys.Up), Down(Keys.Down));
                if (r.RX == 128) r.RX = (byte)Math.Max(0, Math.Min(255, 128 + mouseX * sensitivity.Value * 2));
                if (r.RY == 128) r.RY = (byte)Math.Max(0, Math.Min(255, 128 + mouseY * sensitivity.Value * 2));
                mouseX = mouseY = 0;
                int x = (Down(Keys.NumPad6) ? 1 : 0) - (Down(Keys.NumPad4) ? 1 : 0);
                int y = (Down(Keys.NumPad2) ? 1 : 0) - (Down(Keys.NumPad8) ? 1 : 0);
                int hat = y < 0 ? (x < 0 ? 7 : x > 0 ? 1 : 0) : y > 0 ? (x < 0 ? 5 : x > 0 ? 3 : 4) : (x < 0 ? 6 : x > 0 ? 2 : 8);
                int buttons = hat;
                if (Down(Keys.J)) buttons |= 0x10; if (Down(Keys.K)) buttons |= 0x20; if (Down(Keys.L)) buttons |= 0x40; if (Down(Keys.I)) buttons |= 0x80;
                if (Down(Keys.Q)) buttons |= 0x100; if (Down(Keys.E)) buttons |= 0x200;
                if (Down(Keys.U)) { buttons |= 0x400; r.L2 = 255; } if (Down(Keys.O)) { buttons |= 0x800; r.R2 = 255; }
                if (Down(Keys.Back)) buttons |= 0x1000; if (Down(Keys.Enter)) buttons |= 0x2000;
                if (Down(Keys.D1)) buttons |= 0x4000; if (Down(Keys.D2)) buttons |= 0x8000;
                r.Buttons = (ushort)buttons; if (Down(Keys.P)) r.Special |= 1; if (Down(Keys.T)) r.Special |= 2;
                Check(Native.vigem_target_ds4_update(client, target, r), "傳送輸入"); neutralSent = false;
                status.Text = "狀態：已啟用，正在將按鍵送到虛擬手把。";
            } catch (Exception ex) { CloseDevice(); status.Text = "狀態：控制已停止。"; MessageBox.Show(ex.Message, "手把錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void CloseDevice() {
            active = false; ReleaseMouse();
            if (added) { try { Native.vigem_target_ds4_update(client, target, Neutral()); Native.vigem_target_remove(client, target); } catch {} added = false; }
            if (target != IntPtr.Zero) { Native.vigem_target_free(target); target = IntPtr.Zero; }
            if (connected) { Native.vigem_disconnect(client); connected = false; }
            if (client != IntPtr.Zero) { Native.vigem_free(client); client = IntPtr.Zero; }
            connectButton.Text = "連接虛擬手把"; activeButton.Text = "啟用控制（F8）"; activeButton.Enabled = false;
        }
        [STAThread] static void Main() {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            MainWindow window = new MainWindow();
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e) {
                window.CloseDevice(); MessageBox.Show("控制已停止：" + e.Exception.Message, "程式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate { window.ReleaseMouse(); };
            try { Application.Run(window); } finally { window.CloseDevice(); }
        }
    }
}
