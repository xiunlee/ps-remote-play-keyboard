using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PSKeyboard {
    internal sealed class KeyboardPad : Form {
        private const int ApiPort=33242, UsbPort=33241, ToggleHotkey=0x504B;
        private readonly KeyboardInputState input=new KeyboardInputState();
        private readonly object sessionLock=new object();
        private readonly object focusLock=new object();
        private volatile bool enabled, closing;
        private volatile ViiperTransport transport;
        private Process server;
        private int attachedPort;
        private string attachedBusId;
        private Thread sender;
        private IntPtr hook;
        private HookCallback callback;
        private readonly Button connect=new Button(), toggle=new Button();
        private readonly Label status=new Label();
        private readonly System.Windows.Forms.Timer uiTimer=new System.Windows.Forms.Timer();
        private readonly string baseDir=AppDomain.CurrentDomain.BaseDirectory;
        private readonly string sonyPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Sony","PS Remote Play","RemotePlay.exe");
        private readonly string usbipPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"USBip","usbip.exe");
        private int focusedPid=-1;
        private bool focusedAllowed;
        private long focusedAt;
        private readonly uint ownPid=(uint)Process.GetCurrentProcess().Id;
        private sealed class FocusDecision { public readonly uint Pid; public readonly bool Allowed; public FocusDecision(uint pid,bool allowed){Pid=pid;Allowed=allowed;} }
        private volatile FocusDecision hookFocus=new FocusDecision(0,false);
        private volatile string failure;
        private delegate IntPtr HookCallback(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct KeyData { public uint Key,Scan,Flags,Time; public UIntPtr Extra; }
        [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id,HookCallback callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window,int id);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);

        [STAThread] private static int Main(string[] args) {
            bool created;
            using(Mutex instance=new Mutex(true,"Local\\PSKeyboardUsbMapper",out created)) {
                if(!created) {MessageBox.Show("鍵盤手把程式已開啟。","PS 鍵盤");return 1;}
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new KeyboardPad(args.Length==1 && args[0]=="--connect")); return 0;
            }
        }

        private KeyboardPad(bool autoConnect) {
            Text="PS 鍵盤手把 — USB 測試版";
            ClientSize=new Size(620,410); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
            Font=new Font("Microsoft JhengHei UI",11); BackColor=Color.FromArgb(245,247,251);
            Label title=new Label {Text="鍵盤 → PS4 手把",Font=new Font(Font.FontFamily,21,FontStyle.Bold),AutoSize=true,Location=new Point(24,20)};
            Label note=new Label {Text="鍵盤專用 · 免費開源 · 官方 Remote Play 相容性待驗證",AutoSize=true,Location=new Point(26,65)};
            connect.Text="建立手把"; connect.SetBounds(26,104,180,42); connect.Click+=ConnectClick;
            toggle.Text="啟用鍵盤（F8）"; toggle.SetBounds(222,104,210,42); toggle.Enabled=false; toggle.Click+=delegate {Toggle();};
            status.Text=File.Exists(usbipPath)?"已找到 USBip，可開始建立手把。":"尚未建立手把。需要先安裝 USBip 驅動。"; status.SetBounds(26,163,565,60);
            Label keys=new Label {Text="W A S D　左搖桿        方向鍵　十字鍵\r\nI J L K　△ □ ○ ×       Q E　L1 R1\r\nU O　L2 R2              1 2　L3 R3\r\nEnter　Options          Backspace　Share\r\nT　觸控板按下           P　PS 鍵",Location=new Point(26,235),Size=new Size(565,126)};
            Label bottom=new Label {Text="F8 隨時暫停。只在本視窗或官方 Remote Play 接收按鍵。",Location=new Point(26,374),AutoSize=true,Font=new Font(Font.FontFamily,9)};
            Controls.AddRange(new Control[]{title,note,connect,toggle,status,keys,bottom});
            Shown+=delegate {
                callback=KeyboardHook; hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
                if(hook==IntPtr.Zero || !RegisterHotKey(Handle,ToggleHotkey,0x4000,0x77)) {
                    connect.Enabled=false; status.Text="無法啟用鍵盤或 F8 暫停鍵，程式未接管輸入。";
                    if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
                    return;
                }
                for(int key=0;key<256;key++)
                    if(KeyboardMapping.ActionForKey(key)!=0 && (GetAsyncKeyState(key)&0x8000)!=0)
                        input.Process(key,true,false,false,false);
                if(autoConnect) BeginInvoke(new Action(delegate {ConnectClick(null,EventArgs.Empty);}));
            };
            uiTimer.Interval=100; uiTimer.Tick+=delegate {
                if(failure!=null) {string text=failure;failure=null;enabled=false;ClearKeys();status.Text=text;toggle.Enabled=false;}
                else if(enabled && !InputForeground()) {ClearKeys();status.Text="暫停輸出：切回本視窗或官方 Remote Play 即可操作。";}
                else if(enabled) status.Text="鍵盤已啟用。F8 暫停；Windows／PS5 是否收到仍需實測確認。";
            }; uiTimer.Start();
        }

        private void ClearKeys() {input.Reset();}
        private bool InputForeground() {
            lock(focusLock) {
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            if(pid==ownPid) return true;
            long now=Stopwatch.GetTimestamp();
            if(focusedPid==(int)pid && now-focusedAt<Stopwatch.Frequency/4) return focusedAllowed;
            focusedPid=(int)pid; focusedAt=now; focusedAllowed=false;
            try {using(Process p=Process.GetProcessById((int)pid)) focusedAllowed=String.Equals(p.MainModule.FileName,sonyPath,StringComparison.OrdinalIgnoreCase);} catch { }
            hookFocus=new FocusDecision(pid,focusedAllowed);
            return focusedAllowed;
            }
        }
        private bool HookForeground() {
            uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            FocusDecision decision=hookFocus;
            return pid==ownPid || (decision.Pid==pid && decision.Allowed);
        }
        private IntPtr KeyboardHook(int code,IntPtr message,IntPtr data) {
            if(code>=0) {
                KeyData k=(KeyData)Marshal.PtrToStructure(data,typeof(KeyData));
                bool down=message.ToInt64()==0x100 || message.ToInt64()==0x104;
                bool up=message.ToInt64()==0x101 || message.ToInt64()==0x105;
                // Never capture synthetic input or Windows/Ctrl/Alt shortcuts.
                bool modifiers=(GetAsyncKeyState(0x11)&0x8000)!=0 || (GetAsyncKeyState(0x12)&0x8000)!=0 ||
                    (GetAsyncKeyState(0x5B)&0x8000)!=0 || (GetAsyncKeyState(0x5C)&0x8000)!=0;
                // Low-level callbacks run before the OS updates async key state.
                if(down && (k.Key==0x11 || k.Key==0x12 || k.Key==0x5B || k.Key==0x5C ||
                    k.Key==0xA2 || k.Key==0xA3 || k.Key==0xA4 || k.Key==0xA5)) modifiers=true;
                bool injected=(k.Flags&0x10)!=0;
                if(input.Process((int)k.Key,down,up,injected,enabled && !modifiers && HookForeground()))
                    return new IntPtr(1);
            }
            return CallNextHookEx(hook,code,message,data);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x312 && m.WParam.ToInt32()==ToggleHotkey) {Toggle();return;}
            base.WndProc(ref m);
        }
        private void Toggle() {
            if(closing || !toggle.Enabled) return;
            ViiperTransport current=transport;
            if(current==null || !current.IsConnected) return;
            enabled=!enabled; ClearKeys();
            toggle.Text=enabled?"暫停鍵盤（F8）":"啟用鍵盤（F8）";
            status.Text=enabled?"鍵盤已啟用。":"已暫停，手把按鍵已釋放。";
        }
        private void UpdateUi(Action action) {if(!closing && IsHandleCreated) try{BeginInvoke(action);}catch(InvalidOperationException){} }
        private void ConnectClick(object unused,EventArgs args) {
            if(transport!=null) {enabled=false;ClearKeys();connect.Enabled=false;toggle.Enabled=false;ThreadPool.QueueUserWorkItem(delegate {string cleanup=Cleanup();UpdateUi(delegate {connect.Text="建立手把";connect.Enabled=true;status.Text=cleanup??"手把輸出已停止。";});});return;}
            connect.Enabled=false;status.Text="正在建立手把…";
            ThreadPool.QueueUserWorkItem(delegate {
                lock(sessionLock) {
                try {
                    if(closing) throw new OperationCanceledException();
                    if(!File.Exists(usbipPath)) throw new InvalidOperationException("尚未安裝 USBip 驅動；目前不能建立 Windows 手把。");
                    string viiper=Path.Combine(baseDir,"viiper.exe");
                    if(!File.Exists(viiper)) throw new FileNotFoundException("找不到已驗證的 VIIPER 元件。",viiper);
                    // A port collision is an error. Never use or stop another server.
                    using(System.Net.Sockets.TcpClient check=new System.Net.Sockets.TcpClient()) {
                        IAsyncResult pending=check.BeginConnect("127.0.0.1",ApiPort,null,null);
                        if(pending.AsyncWaitHandle.WaitOne(100)) {try{check.EndConnect(pending);throw new InvalidOperationException("測試通道仍被使用，請先關閉先前的 PS 鍵盤程式。");}catch(System.Net.Sockets.SocketException){} }
                    }
                    server=Process.Start(new ProcessStartInfo(viiper,"server --api.addr=127.0.0.1:"+ApiPort+" --usb.addr=127.0.0.1:"+UsbPort+" --api.auto-attach-local-client=false --no-tray --update-notify=none") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=baseDir});
                    Exception last=null;
                    for(int n=0;n<30;n++) {
                        if(closing) throw new OperationCanceledException();
                        if(server.HasExited) throw new InvalidOperationException("手把服務無法啟動。");
                        try {transport=new ViiperTransport(ApiPort);transport.Connect();last=null;break;}
                        catch(Exception ex) {last=ex;if(transport!=null)transport.Dispose();transport=null;Thread.Sleep(100);}
                    }
                    if(last!=null || transport==null) throw new InvalidOperationException("無法連接手把服務。",last);
                    if(closing) throw new OperationCanceledException();
                    attachedBusId=transport.UsbBusId;
                    string output=Usbip("--tcp-port "+UsbPort+" attach --remote 127.0.0.1 --bus-id "+attachedBusId+" --once --terse");
                    int parsedPort;
                    if(!Int32.TryParse(output.Trim(),out parsedPort) || parsedPort<=0)
                        throw new InvalidOperationException("USB 驅動未回報有效的手把連接埠。");
                    attachedPort=parsedPort;
                    if(closing) throw new OperationCanceledException();
                    // The CLI attach result is not proof of Remote Play acceptance.
                    sender=new Thread(SendLoop){IsBackground=true,Name="PSKeyboardStateSender"};sender.Start();
                    UpdateUi(delegate {connect.Text="移除手把";connect.Enabled=true;toggle.Enabled=true;status.Text="USB 手把已要求接入 Windows。按 F8 啟用鍵盤。";});
                } catch(Exception ex) {string cleanup=Cleanup();UpdateUi(delegate {connect.Enabled=true;status.Text=ex.Message+(cleanup==null?"":"\r\n"+cleanup);});}
                }
            });
        }
        private string Usbip(string arguments) {
            using(Process p=Process.Start(new ProcessStartInfo(usbipPath,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
                StringBuilder output=new StringBuilder();
                p.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null)lock(output)output.AppendLine(e.Data);};
                p.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null)lock(output)output.AppendLine(e.Data);};
                p.BeginOutputReadLine();p.BeginErrorReadLine();
                if(!p.WaitForExit(10000)) {try{p.Kill();}catch{} throw new TimeoutException("USB 手把接入逾時。");}
                p.WaitForExit();
                if(p.ExitCode!=0) throw new InvalidOperationException("USB 手把接入失敗："+output.ToString().Trim());
                return output.ToString();
            }
        }
        private void SendLoop() {
            ViiperTransport session=transport;
            try {
                while(!closing && session!=null && Object.ReferenceEquals(transport,session) && session.IsConnected) {
                    bool active=enabled && InputForeground();
                    if(!active) ClearKeys(); uint keys=input.Held;
                    session.Send(KeyboardMapping.Frame(keys,active));Thread.Sleep(8);
                }
                if(!closing && session!=null && Object.ReferenceEquals(transport,session)) throw new IOException("手把輸入通道已中斷。");
            } catch(Exception ex) {enabled=false;ClearKeys();failure="輸出已停止："+ex.Message;}
        }
        private string Cleanup() {
            lock(sessionLock) {
            string warning=null;
            enabled=false;ClearKeys();
            ViiperTransport old=transport;transport=null;
            if(sender!=null && sender!=Thread.CurrentThread) sender.Join(2500);sender=null;
            if(old!=null && old.IsConnected) try{old.Send(ViiperTransport.CreateNeutralFrame());}catch{}
            if(attachedPort>0) {
                try {
                    string detail=Usbip("port "+attachedPort);
                    string ownLocation=@"(?m)^\s*-> usbip://127\.0\.0\.1:"+UsbPort+"/"+Regex.Escape(attachedBusId)+@"\s*$";
                    // Ports can be reused. Never detach one now owned by another device.
                    if(Regex.IsMatch(detail,ownLocation)) Usbip("detach --port "+attachedPort);
                } catch(Exception ex) {warning="輸出已停止，USB 移除狀態未確認："+ex.Message;}
                attachedPort=0;
            }
            if(old!=null) {try{old.Dispose();}catch(Exception ex){warning="手把通道清理失敗："+ex.Message;}}
            if(server!=null) {try{if(!server.HasExited)server.Kill();server.Dispose();}catch{}server=null;}
            attachedBusId=null;
            return warning;
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            closing=true;enabled=false;ClearKeys();uiTimer.Stop();
            UnregisterHotKey(Handle,ToggleHotkey);if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
            Cleanup();base.OnFormClosing(e);
        }
    }
}
