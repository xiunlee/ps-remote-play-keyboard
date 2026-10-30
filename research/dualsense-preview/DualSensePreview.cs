using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    // Fixed DualSense gamepad transport. This never opens a physical controller,
    // modifies Sony, starts/stops Sony, or installs a driver.
    internal sealed class DualSensePreview : Form
    {
        private const int ApiPort = 33642, UsbPort = 33641, ToggleHotkey = 0x504D;
        private const string UsbipHash = "817DA219FA153B4B37C3BC1AA8FCC63F33E046188C57CC3DD2C60A6EF0883C56";
        private const string UsbipDllHash = "F123EF5D288B817A890CBE39A3C2D212B3D8B1765C691C8703CCB76C7337986C";
        internal const string Persona = "dualsensegamepadv5";
        // The neutral main-window palette is local to this preview. The older PS4
        // interface and the shared binding editor keep their existing styling.
        private static readonly Color Canvas = Color.FromArgb(248, 248, 246);
        private static readonly Color MainInk = Color.FromArgb(38, 38, 38);
        private static readonly Color MainMuted = Color.FromArgb(96, 96, 96);
        private static readonly Color MainBorder = Color.FromArgb(215, 215, 212);
        private static readonly Color MainSurface = Color.White;
        private static string ModeLabel { get { return PreviewText.T("ModeLabel"); } }
        internal static string WindowTitle { get { return UiText.T("AppTitle") + " V " + BuildInfo.Version; } }
        private readonly object sessionGate = new object(), logGate = new object(), focusGate = new object();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly KeyboardControlPipeline input;
        private volatile BindingProfile profile;
        private readonly string profilePath, statusPath;
        private readonly string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string sonyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sony", "PS Remote Play", "RemotePlay.exe");
        private readonly string usbipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "USBip", "usbip.exe");
        private readonly int ownPid = Process.GetCurrentProcess().Id;
        private readonly Button connect = new PillButton(), toggle = new PillButton(), configure = new PillButton();
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
            UiText.Initialize(CultureInfo.CurrentUICulture);
            if (args.Length > 0 && (args[0] == "--render-preview" || args[0] == "--render-editor-preview")) return RenderPreview(args);
            bool autoConnect; string statusFile, deviceType;
            try { ParseOptions(args, out autoConnect, out statusFile, out deviceType); }
            catch (ArgumentException)
            {
                MessageBox.Show(PreviewText.T("Usage"), WindowTitle); return 2;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\PSKeyboardControllerFreeExperiment", out created))
            {
                if (!created) { MessageBox.Show(PreviewText.T("AlreadyRunning"), WindowTitle); return 1; }
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
            autoConnect = false; statusFile = null; deviceType = Persona; bool typeSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--status-file" && statusFile == null && i + 1 < args.Length) statusFile = ValidateStatusPath(args[++i]);
                else if (args[i] == "--persona" && !typeSet && i + 1 < args.Length)
                {
                    deviceType = args[++i];
                    if (deviceType != Persona) throw new ArgumentException("Only the selected DualSense gamepad mode is supported.");
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
                using (Form form = args[0] == "--render-editor-preview" ? (Form)new BindingEditor(BindingProfile.Default(), true) :
                    CreateOfflinePreview())
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
        // This constructor path never reads profiles or installs hooks, timers or devices.
        internal static Form CreateOfflinePreview()
        { return new DualSensePreview(false, null, Persona, true); }

        private DualSensePreview(bool autoConnect, string statusFile, string deviceType, bool renderPreview)
        {
            preview = renderPreview; statusPath = preview ? null : statusFile; controllerType = deviceType;
            profilePath = preview ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSKeyboard", "bindings.json");
            profile = BindingProfile.Default(); bool badProfile = false;
            if (!preview) try { profile = BindingProfile.Load(profilePath); } catch { badProfile = true; }
            input = new KeyboardControlPipeline(delegate { return profile; });
            Text = WindowTitle; ClientSize = new Size(960, 840); MinimumSize = new Size(900, 720);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Canvas; ForeColor = MainInk; Font = KeyboardUi.Font(10, FontStyle.Regular);
            Icon brandIcon = KeyboardUi.LoadAppIcon(); if (brandIcon != null) { Icon = brandIcon; Disposed += delegate { brandIcon.Dispose(); }; }
            BuildInterface(badProfile);
            if (preview)
            {
                status.Text = PreviewText.T("Disconnected"); connectionDot.DotColor = KeyboardUi.Gray;
                keyboardStatus.Text = PreviewText.T("InitiallyPaused"); keyboardDot.DotColor = KeyboardUi.Gray;
                return;
            }
            Shown += delegate {
                callback = KeyboardHook; hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                hotkeyRegistered = hook != IntPtr.Zero && RegisterHotKey(Handle, ToggleHotkey, 0x4000, 0x77);
                if (!hotkeyRegistered)
                {
                    failureCode = "keyboard_setup_failed"; connect.Enabled = false;
                    status.Text = PreviewText.T("FailureKeyboardSetup");
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
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(shell);
            TableLayoutPanel hero = AutoTable(3, 1); hero.BackColor = Canvas; hero.Padding = new Padding(28, 20, 28, 12); hero.MinimumSize = new Size(0, 120);
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            Image mark = KeyboardUi.LoadBrandMark();
            PictureBox brand = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = mark, BackColor = KeyboardUi.Accent, MinimumSize = new Size(0, 70), Margin = new Padding(0, 0, 18, 0), Padding = new Padding(7) };
            if (mark != null) brand.Disposed += delegate { mark.Dispose(); };
            hero.Controls.Add(brand, 0, 0);
            TableLayoutPanel wordmark = AutoTable(1, 3);
            Label eyebrow = WrappedLabel(PreviewText.T("Eyebrow"), 9, MainMuted);
            Label title = WrappedLabel(UiText.T("AppTitle"), 23, MainInk); title.Font = KeyboardUi.Font(23, FontStyle.Bold);
            Label subtitle = WrappedLabel(UiText.T("AppSubtitle"), 10, MainMuted);
            wordmark.Controls.Add(eyebrow, 0, 0); wordmark.Controls.Add(title, 0, 1); wordmark.Controls.Add(subtitle, 0, 2); hero.Controls.Add(wordmark, 1, 0);
            Label version = WrappedLabel("V " + BuildInfo.Version, 10, MainMuted); version.Font = KeyboardUi.CodeFont(10, FontStyle.Regular); version.TextAlign = ContentAlignment.MiddleRight; hero.Controls.Add(version, 2, 0);
            shell.Controls.Add(hero, 0, 0);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 22, 28, 18), Margin = Padding.Empty };
            TableLayoutPanel body = AutoTable(1, 8);
            scroll.Controls.Add(body); shell.Controls.Add(scroll, 0, 1);
            TableLayoutPanel mode = AutoTable(2, 1); mode.Margin = new Padding(0, 0, 0, 18); mode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); mode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            Label modeTitle = WrappedLabel(PreviewText.T("ModeHeading"), 12, MainInk); modeTitle.Font = KeyboardUi.Font(12, FontStyle.Bold); modeTitle.Margin = new Padding(0, 0, 12, 0);
            Label fixedMode = WrappedLabel(ModeLabel, 12, MainInk);
            mode.Controls.Add(modeTitle, 0, 0); mode.Controls.Add(fixedMode, 1, 0); body.Controls.Add(mode, 0, 0);
            TableLayoutPanel states = AutoTable(2, 1); states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); states.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            states.Controls.Add(StatusCard(PreviewText.T("ConnectionHeading"), status, connectionDot, new Padding(0, 0, 12, 12)), 0, 0);
            states.Controls.Add(StatusCard(UiText.T("KeyboardTitle"), keyboardStatus, keyboardDot, new Padding(0, 0, 0, 12)), 1, 0); body.Controls.Add(states, 0, 1);
            status.Text = PreviewText.T("Disconnected"); keyboardStatus.Text = PreviewText.T("InitiallyPaused");
            FlowLayoutPanel actions = AutoActions(); actions.Margin = new Padding(0, 0, 0, 12);
            MainButton(toggle, UiText.T("EnableKeyboard"), false); MainButton(configure, PreviewText.T("SettingsButton"), false); MainButton(connect, PreviewText.T("ConnectButton"), true);
            toggle.Enabled = false; connect.Click += ConnectClick; toggle.Click += delegate { Toggle(); }; configure.Click += delegate { EditBindings(); };
            actions.Controls.AddRange(new Control[] { toggle, configure, connect }); body.Controls.Add(actions, 0, 2);
            TableLayoutPanel guide = AutoTable(4, 2); guide.BackColor = MainSurface; guide.Padding = new Padding(18, 12, 18, 12); guide.Margin = new Padding(0, 0, 0, 12);
            Label guideTitle = WrappedLabel(PreviewText.T("BindingsHeading"), 11, MainInk); guideTitle.Font = KeyboardUi.Font(11, FontStyle.Bold); guideTitle.Margin = new Padding(0, 0, 0, 12);
            guide.Controls.Add(guideTitle, 0, 0); guide.SetColumnSpan(guideTitle, 4);
            for (int i = 0; i < 4; i++) { guide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); keySummaries[i] = WrappedLabel("", 10, MainInk); keySummaries[i].Margin = new Padding(0, 0, 12, 0); guide.Controls.Add(keySummaries[i], i, 1); }
            UpdateBindingSummary(); body.Controls.Add(guide, 0, 3);
            Label help = WrappedLabel(PreviewText.T("ControlsHelp") + "\r\n" + PreviewText.T("GameSuitability"), 9, MainMuted); help.Margin = new Padding(0, 0, 0, 12); body.Controls.Add(help, 0, 4);
            notice.Text = badProfile ? UiText.T("ProfileLoadWarning") : PreviewText.T("ProfilePreserved");
            notice.ForeColor = badProfile ? KeyboardUi.Amber : MainMuted; notice.Font = KeyboardUi.Font(9, FontStyle.Regular); notice.AutoSize = true; notice.Dock = DockStyle.Fill; notice.TextAlign = ContentAlignment.MiddleLeft; notice.Margin = new Padding(0, 0, 0, 12); body.Controls.Add(notice, 0, 5);
            Label connectionScope = WrappedLabel(PreviewText.T("ConnectionScope"), 9, MainMuted); connectionScope.Margin = new Padding(0, 0, 0, 12); body.Controls.Add(connectionScope, 0, 6);
            TableLayoutPanel support = AutoTable(1, 4); support.BackColor = MainSurface; support.Padding = new Padding(18, 14, 18, 10); support.Margin = new Padding(0, 8, 0, 0);
            Label free = WrappedLabel(PreviewText.T("FreeOpenSource"), 10, MainInk); free.Font = KeyboardUi.Font(10, FontStyle.Bold); free.Margin = new Padding(0, 0, 0, 8);
            Label cats = WrappedLabel(PreviewText.T("CatsSupport"), 10, MainMuted); cats.Margin = new Padding(0, 0, 0, 12);
            Label independent = WrappedLabel(PreviewText.T("IndependentNotice"), 9, MainMuted); independent.Margin = new Padding(0, 0, 0, 12);
            FlowLayoutPanel supportActions = AutoActions();
            Button sponsor = new PillButton(), feedback = new PillButton();
            MainButton(sponsor, PreviewText.T("SponsorButton"), false); MainButton(feedback, PreviewText.T("FeedbackButton"), false);
            sponsor.Click += delegate { OpenSupportLink(true); }; feedback.Click += delegate { OpenSupportLink(false); };
            if (SupportLinks.SponsorAvailable) supportActions.Controls.Add(sponsor); else sponsor.Dispose();
            supportActions.Controls.Add(feedback);
            if (!SupportLinks.SponsorAvailable)
            {
                Label pending = WrappedLabel(PreviewText.T("SponsorUnavailable"), 9, MainMuted); pending.Dock = DockStyle.None; pending.Margin = new Padding(0, 12, 0, 0); supportActions.Controls.Add(pending);
            }
            support.Controls.Add(free, 0, 0); support.Controls.Add(cats, 0, 1); support.Controls.Add(independent, 0, 2); support.Controls.Add(supportActions, 0, 3); body.Controls.Add(support, 0, 7);
        }

        private static void MainButton(Button button, string text, bool primary)
        {
            KeyboardUi.Button(button, text, primary);
            Action colors = delegate {
                button.BackColor = !button.Enabled ? Color.FromArgb(229, 229, 229) : primary ? MainInk : MainSurface;
                button.ForeColor = !button.Enabled ? Color.FromArgb(110, 110, 110) : primary ? Color.White : MainInk;
                button.FlatAppearance.BorderColor = !button.Enabled ? Color.FromArgb(229, 229, 229) : primary ? MainInk : MainBorder;
            };
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(65, 65, 65) : Color.FromArgb(238, 238, 236);
            button.FlatAppearance.MouseDownBackColor = primary ? Color.Black : Color.FromArgb(225, 225, 223);
            button.EnabledChanged += delegate { colors(); };
            colors();
        }

        private sealed class PillButton : Button
        {
            private bool hovered, pressed;
            internal PillButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
            protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Control parent = Parent;
                while (parent != null && parent.BackColor == Color.Transparent) parent = parent.Parent;
                e.Graphics.Clear(parent == null ? Canvas : parent.BackColor);
                if (Width <= 2 || Height <= 2) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int diameter = Math.Min(Height - 2, Width - 2);
                using (GraphicsPath path = new GraphicsPath()) {
                    path.AddArc(1, 1, diameter, diameter, 90, 180);
                    path.AddArc(Width - diameter - 1, 1, diameter, diameter, 270, 180);
                    path.CloseFigure();
                    Color fill = Enabled && pressed ? FlatAppearance.MouseDownBackColor : Enabled && hovered ? FlatAppearance.MouseOverBackColor : BackColor;
                    using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                    using (Pen border = new Pen(FlatAppearance.BorderColor)) e.Graphics.DrawPath(border, path);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -8, -7));
            }
        }

        private static TableLayoutPanel AutoTable(int columns, int rows)
        {
            TableLayoutPanel table = KeyboardUi.Table(columns); table.RowCount = rows;
            table.AutoSize = true; table.AutoSizeMode = AutoSizeMode.GrowAndShrink; table.Dock = DockStyle.Top;
            if (columns == 1) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return table;
        }
        private static Label WrappedLabel(string text, float size, Color color)
        {
            Label label = KeyboardUi.Label(text, size, color); label.AutoSize = true; label.Dock = DockStyle.Fill;
            return label;
        }
        private static FlowLayoutPanel AutoActions()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, WrapContents = true };
            flow.ControlAdded += delegate(object sender, ControlEventArgs e) { e.Control.Margin = new Padding(0, 0, 12, 8); };
            return flow;
        }

        private void OpenSupportLink(bool sponsorship)
        {
            // Only the two explicit Click handlers call this; rendering cannot launch anything.
            if (preview || closing || editing) return;
            string target = sponsorship ? SupportLinks.SponsorUrl : SupportLinks.FeedbackUrl(BuildInfo.Version);
            if (!SupportLinks.IsAllowedLaunchUrl(target, BuildInfo.Version)) return;
            try { using (Process opened = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })) { } }
            catch (System.ComponentModel.Win32Exception) { SupportLinkFailure(sponsorship); }
            catch (InvalidOperationException) { SupportLinkFailure(sponsorship); }
        }
        private void SupportLinkFailure(bool sponsorship)
        {
            MessageBox.Show(this, sponsorship ? PreviewText.T("SponsorFailure") :
                PreviewText.F("FeedbackFailure", SupportLinks.FeedbackAddress), WindowTitle);
        }

        private static Control StatusCard(string heading, Label value, StatusDot dot, Padding margin)
        {
            TableLayoutPanel card = AutoTable(2, 2); card.BackColor = MainSurface; card.Padding = new Padding(16, 10, 14, 14); card.Margin = margin;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dot.Margin = new Padding(0, 3, 0, 0); card.Controls.Add(dot, 0, 0);
            Label title = WrappedLabel(heading, 9, MainMuted); title.Margin = new Padding(0, 0, 0, 8); card.Controls.Add(title, 1, 0);
            value.AutoSize = true; value.Dock = DockStyle.Fill; value.Font = KeyboardUi.Font(10, FontStyle.Regular); value.TextAlign = ContentAlignment.MiddleLeft; value.Margin = Padding.Empty;
            card.Controls.Add(value, 1, 1); return card;
        }
        private string KeyNames(params int[] actions)
        {
            string[] names = new string[actions.Length]; for (int i = 0; i < actions.Length; i++) names[i] = UiText.KeyName(profile.KeyForAction(actions[i]));
            return String.Join(" / ", names);
        }
        private void UpdateBindingSummary()
        {
            keySummaries[0].Text = PreviewText.T("LeftStick") + "\r\n" + KeyNames(0, 2, 1, 3);
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
            toggle.Text = UiText.T(enabled ? "PauseKeyboard" : "EnableKeyboard");
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
                if (editing && activeEditor != null) activeEditor.ReservedHotkeyPressed(); else Toggle(); return;
            }
            base.WndProc(ref message);
        }
        private void EditBindings()
        {
            if (preview || closing || editing || busy) return;
            editing = true; SetEnabled(false);
            try
            {
                using (BindingEditor editor = new BindingEditor(profile))
                {
                    activeEditor = editor;
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    try { profile = SaveEditedProfile(editor.Result, profilePath); }
                    catch { MessageBox.Show(this, UiText.T("ProfileSaveFailure"), UiText.T("BindingsTitle")); return; }
                    input.Reset(); UpdateBindingSummary();
                    notice.Text = PreviewText.T("ProfileSaved");
                }
            }
            finally { activeEditor = null; SetEnabled(false); editing = false; RefreshStatus(); }
        }
        // The immutable draft is published only after persistence succeeds.
        internal static BindingProfile SaveEditedProfile(BindingProfile edited, string destination)
        {
            if (edited == null) throw new ArgumentNullException("edited");
            edited.Save(destination);
            return edited;
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
                status.Text = FailureMessage(failureCode) + (cleanupCode == null ? "" : PreviewText.T("CleanupSuffix"));
            }
            else if (cleanupBlocked) status.Text = PreviewText.T("CleanupBlocked");
            connectionDot.DotColor = failureCode != null ? KeyboardUi.Red : busy ? KeyboardUi.Amber : usbAttachedVerified && current != null && current.IsConnected ? KeyboardUi.Green : KeyboardUi.Gray;
            keyboardDot.DotColor = !enabled ? KeyboardUi.Gray : !foreground || ModifiersDown() ? KeyboardUi.Amber : KeyboardUi.Accent;
            keyboardStatus.Text = PreviewText.T(!enabled ? "KeyboardPaused" : !foreground || ModifiersDown() ? "KeyboardWaiting" : "KeyboardActive");
        }
        internal static string FailureMessage(string code)
        {
            switch (code)
            {
                case "keyboard_setup_failed": return PreviewText.T("FailureKeyboardSetup");
                case "usbip_missing": return PreviewText.T("FailureUsbipMissing");
                case "usbip_invalid": return PreviewText.T("FailureUsbipInvalid");
                case "existing_import": return PreviewText.T("FailureExistingImport");
                case "backend_invalid": return PreviewText.T("FailureBackendInvalid");
                case "ports_unavailable": return PreviewText.T("FailurePortsUnavailable");
                case "usb_attach_failed": return PreviewText.T("FailureUsbAttach");
                case "send_failed": return PreviewText.T("FailureSend");
                case "cancelled": return PreviewText.T("FailureCancelled");
                case "backend_start_failed": return PreviewText.T("FailureBackendStart");
                case "backend_exited": return PreviewText.T("FailureBackendExited");
                case "usbip_timeout": return PreviewText.T("FailureUsbipTimeout");
                case "usbip_output_limit": return PreviewText.T("FailureUsbipOutput");
                default: return PreviewText.T("FailureConnect");
            }
        }

        private void UpdateUi(Action action)
        { if (!closing && IsHandleCreated) try { BeginInvoke(action); } catch (InvalidOperationException) { } }
        internal static bool CanReconnect(bool cleanupUnknown, bool attachUnresolved, bool commandUnconfirmed)
        { return !cleanupUnknown && !attachUnresolved && !commandUnconfirmed; }
        internal static bool HasCandidateImport(string listing)
        { return (listing ?? "").IndexOf("usbip://127.0.0.1:" + UsbPort + "/", StringComparison.OrdinalIgnoreCase) >= 0; }
        private void ConnectClick(object senderObject, EventArgs args)
        {
            if (closing || editing || busy || !hotkeyRegistered) return;
            if (transport == null && server == null && !CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed))
            { status.Text = PreviewText.T("PreviousRemovalUnknown"); return; }
            if (transport != null || server != null)
            {
                SetEnabled(false); busy = true; connect.Enabled = toggle.Enabled = configure.Enabled = false;
                ThreadPool.QueueUserWorkItem(delegate { Cleanup(); UpdateUi(delegate { busy = false; configure.Enabled = true; connect.Enabled = CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed); connect.Text = PreviewText.T("ConnectButton"); status.Text = PreviewText.T(cleanupCode == null ? "ControllerDisconnected" : "CleanupIncomplete"); }); });
                return;
            }
            busy = true; connect.Enabled = configure.Enabled = false; failureCode = cleanupCode = null; attachReturned = false; SetEnabled(false); status.Text = PreviewText.F("Connecting", ModeLabel);
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
                        UpdateUi(delegate { busy = false; connect.Enabled = configure.Enabled = toggle.Enabled = true; connect.Text = PreviewText.T("DisconnectButton"); status.Text = PreviewText.F("ControllerAttached", ModeLabel); });
                    }
                    catch (Exception ex)
                    {
                        ExperimentException expected = ex as ExperimentException;
                        failureCode = expected == null ? "connect_failed" : expected.Code;
                        RecordDiagnostic("connect_failure", ex.GetType().Name); Cleanup();
                        UpdateUi(delegate { busy = false; configure.Enabled = true; connect.Enabled = CanReconnect(cleanupBlocked, attachAttempted, usbipCommandTerminationUnconfirmed); connect.Text = PreviewText.T("ConnectButton"); RefreshStatus(); });
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
