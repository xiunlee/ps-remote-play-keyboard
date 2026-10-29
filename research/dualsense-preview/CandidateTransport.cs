// Isolated candidate transport. Device creation is not Windows/Sony acceptance.
// No driver operations, keyboard capture, settings access, or automatic attach.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PSKeyboard.Research;

namespace PSKeyboard
{
    public sealed class CandidateTransport : IDisposable
    {
        public const int DefaultPort = 33642;
        public const int FrameSize = 33;
        const int MaxResponseBytes = 16384;
        readonly object gate = new object();
        readonly int port, timeoutMs, settleMs;
        readonly string persona;
        readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        volatile Session active;
        volatile string lastFailure, serverVersion;
        bool disposed;
        uint busId;
        string deviceId;
        long feedbackPackets;

        sealed class Session
        {
            internal readonly TcpClient Client;
            internal readonly NetworkStream Stream;
            internal readonly V5FeedbackReader Feedback;
            internal volatile bool Ready, Failed;
            internal uint InputSequence;
            internal Thread Reader;
            internal Session(TcpClient client)
            {
                Client = client; Stream = client.GetStream();
                // All four pinned personas share this framing. The codec accepts
                // the optional source-defined microphone event, never raw bytes.
                Feedback = new V5FeedbackReader(Stream, true);
            }
            internal void Close()
            { Failed = true; Ready = false; Client.Close(); }
        }

        public CandidateTransport(int port, string persona) : this(port, persona, 1500, 100, false) { }
        // Tests bind an ephemeral loopback fake server; production callers cannot
        // redirect this candidate at the working PS4 API or another live service.
        internal CandidateTransport(int port, string persona, int timeoutMs, int settleMs)
            : this(port, persona, timeoutMs, settleMs, true) { }
        CandidateTransport(int port, string persona, int timeoutMs, int settleMs, bool isolatedTest)
        {
            if (port < 1 || port > 65535 || (!isolatedTest && port != DefaultPort))
                throw new ArgumentOutOfRangeException("port", "The candidate API is fixed at loopback port 33642.");
            if (timeoutMs < 50 || timeoutMs > 10000) throw new ArgumentOutOfRangeException("timeoutMs");
            if (settleMs < 0 || settleMs > timeoutMs) throw new ArgumentOutOfRangeException("settleMs");
            this.port = port; this.timeoutMs = timeoutMs; this.settleMs = settleMs;
            this.persona = ValidatePersona(persona);
        }
        public static string ValidatePersona(string value)
        {
            switch (value) {
                case "dualsensecombinedaudioduplexv5": case "dualsensegamepadv5":
                case "dualsenseedgecombinedaudioduplexv5": case "dualsenseedgegamepadv5": return value;
                default: throw new ArgumentException("Only the four exact, tested V5 personas are allowed.", "value");
            }
        }
        public string DeviceType { get { return "dualsense"; } }
        public string Persona { get { return persona; } }
        public int InputFrameSize { get { return FrameSize; } }
        public bool IsConnected { get { Session s = active; return s != null && s.Ready && !s.Failed; } }
        public string LastFailure { get { return lastFailure; } }
        public string ServerVersion { get { return serverVersion; } }
        public long FeedbackPackets { get { return Interlocked.Read(ref feedbackPackets); } }
        public uint BusId { get { lock (gate) { return busId; } } }
        public string DeviceId { get { lock (gate) { return deviceId; } } }
        public string UsbBusId { get { lock (gate) { return busId == 0 || deviceId == null ? null : BusNumber() + "-" + deviceId; } } }
        public byte[] NeutralFrame() { var frame = new byte[FrameSize]; frame[32] = 0xe0; return frame; }
        public void SendNeutral() { Send(NeutralFrame()); }

        public void Connect()
        {
            lock (gate) {
                if (disposed) throw new ObjectDisposedException("CandidateTransport");
                if (IsConnected) return;
                DisconnectCore(false);
                if (busId != 0) throw new IOException("Previous candidate bus cleanup is not confirmed: " + lastFailure);
                lastFailure = null; serverVersion = null;
                try {
                    Dictionary<string, object> ping = Request("ping"); object autoAttach;
                    if (Text(ping, "labMarker") != "pskeyboard-dualsense-v5-candidate" ||
                        Text(ping, "sourceCommit") != V5Protocol.UpstreamCommit ||
                        Text(ping, "version") != "research.preview.1" ||
                        Text(ping, "apiAddress") != "127.0.0.1:33642" || Text(ping, "usbAddress") != "127.0.0.1:33641" ||
                        !ping.TryGetValue("autoAttach", out autoAttach) || !(autoAttach is bool) || (bool)autoAttach)
                        throw new InvalidDataException("The local service is not the pinned candidate with auto-attach disabled.");
                    serverVersion = Text(ping, "version");
                    if (String.IsNullOrWhiteSpace(serverVersion)) throw new InvalidDataException("Candidate version is missing.");
                    busId = Number(Request("bus/create"), "busId");
                    if (busId == 0) throw new InvalidDataException("Candidate returned an invalid owned bus.");
                    Dictionary<string, object> added = Request("bus/" + BusNumber() + "/add {\"type\":\"" + persona + "\"}");
                    if (Number(added, "busId") != busId || Text(added, "type") != persona)
                        throw new InvalidDataException("Candidate returned a different bus or persona.");
                    string id = Text(added, "devId"); uint number;
                    if (!UInt32.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0)
                        throw new InvalidDataException("Candidate returned an invalid device ID.");
                    deviceId = number.ToString(CultureInfo.InvariantCulture);
                    var timer = Stopwatch.StartNew();
                    Session current = new Session(OpenConnection(timer)); active = current;
                    WriteCommand(current.Stream, "bus/" + BusNumber() + "/" + deviceId, timer);
                    // There is no positive stream ACK. Immediate EOF, JSON errors,
                    // and invalid V5 frames must fail before the ready state.
                    if (current.Client.Client.Poll(settleMs * 1000, SelectMode.SelectRead)) {
                        current.Feedback.Read(timeoutMs); Interlocked.Increment(ref feedbackPackets);
                    }
                    WriteFrame(current, V5Protocol.ConvertLegacyKeyboardState(NeutralFrame()));
                    current.Ready = true;
                    current.Reader = new Thread(delegate() { ReadFeedback(current); });
                    current.Reader.IsBackground = true; current.Reader.Name = "Candidate V5 feedback"; current.Reader.Start();
                } catch (Exception error) {
                    lastFailure = "Candidate connect failed: " + error.Message;
                    DisconnectCore(false);
                    throw new IOException(lastFailure, error);
                }
            }
        }
        public void Send(byte[] legacy33)
        {
            // Validation and copying precede lock/wire mutation. Invalid caller
            // data cannot consume a sequence or corrupt the active connection.
            byte[] native = V5Protocol.ConvertLegacyKeyboardState(legacy33);
            lock (gate) {
                if (disposed) throw new ObjectDisposedException("CandidateTransport");
                Session current = active;
                if (current == null || !current.Ready || current.Failed)
                    throw new IOException("Candidate stream is disconnected. " + lastFailure);
                try { WriteFrame(current, native); }
                catch (Exception error) {
                    lastFailure = "Candidate input send failed: " + error.Message;
                    current.Failed = true; DisconnectCore(false); throw new IOException(lastFailure, error);
                }
            }
        }
        public void Disconnect() { lock (gate) { DisconnectCore(true); } }
        public void Dispose()
        {
            Thread reader;
            lock (gate) {
                reader = active == null ? null : active.Reader;
                // A second Dispose retries a known bus whose cleanup previously
                // failed. The ID is never erased merely because the UI closed.
                DisconnectCore(!disposed); disposed = true;
            }
            if (reader != null && reader != Thread.CurrentThread && !reader.Join(timeoutMs))
            {
                lock (gate) { RememberFailure("Feedback reader did not stop within the cleanup deadline."); }
                // The caller must latch uncertain cleanup even when bus removal
                // succeeded. A diagnostic string alone must not permit reconnect.
                throw new IOException("Candidate feedback reader cleanup is unconfirmed.");
            }
        }
        void DisconnectCore(bool neutral)
        {
            Session current = active;
            if (current != null) {
                if (neutral && current.Ready && !current.Failed) {
                    try { WriteFrame(current, V5Protocol.ConvertLegacyKeyboardState(NeutralFrame())); }
                    catch (Exception error) { RememberFailure("Neutral delivery failed: " + error.Message); }
                }
                active = null;
                try { current.Close(); } // Unblocks Poll or a partial frame read.
                catch (Exception error) { RememberFailure("Stream close failed: " + error.Message); }
            }
            if (busId == 0) { deviceId = null; return; }
            try {
                var response = Request("bus/remove " + BusNumber());
                if (Number(response, "busId") != busId) throw new InvalidDataException("Removed bus identity mismatch.");
                busId = 0; deviceId = null;
            } catch (CandidateApiException error) {
                if (error.Status == 404) { busId = 0; deviceId = null; }
                else RememberFailure("Own bus cleanup failed: " + error.Message);
            } catch (Exception error) { RememberFailure("Own bus cleanup failed: " + error.Message); }
        }
        void ReadFeedback(Session current)
        {
            try {
                while (!current.Failed) {
                    // An idle controller need not produce feedback. Start the
                    // bounded frame reader only after readable data/EOF appears.
                    if (!current.Client.Client.Poll(250000, SelectMode.SelectRead)) continue;
                    current.Feedback.Read(timeoutMs); Interlocked.Increment(ref feedbackPackets);
                }
            } catch (Exception error) {
                current.Failed = true; // IsConnected fails immediately, even if Send holds gate.
                lock (gate) {
                    if (!Object.ReferenceEquals(active, current)) return;
                    lastFailure = "Candidate feedback disconnected: " + error.Message;
                    DisconnectCore(false);
                }
            }
        }
        void WriteFrame(Session current, byte[] native)
        {
            byte[] packet = V5Protocol.EncodeInput(native, current.InputSequence++);
            current.Stream.WriteTimeout = timeoutMs; var timer = Stopwatch.StartNew();
            current.Stream.Write(packet, 0, packet.Length); Remaining(timer);
        }
        void RememberFailure(string detail)
        { lastFailure = String.IsNullOrEmpty(lastFailure) ? detail : lastFailure + " | " + detail; }
        string BusNumber() { return busId.ToString(CultureInfo.InvariantCulture); }
        int Remaining(Stopwatch timer)
        {
            int remaining = timeoutMs - (int)timer.ElapsedMilliseconds;
            if (remaining <= 0) throw new TimeoutException("Candidate operation exceeded its deadline.");
            return remaining;
        }
        TcpClient OpenConnection(Stopwatch timer)
        {
            var result = new TcpClient(AddressFamily.InterNetwork);
            try {
                var pending = result.BeginConnect(IPAddress.Loopback, port, null, null);
                using (WaitHandle done = pending.AsyncWaitHandle) {
                    if (!done.WaitOne(Remaining(timer))) throw new TimeoutException("Candidate loopback connection timed out.");
                    result.EndConnect(pending);
                }
                Remaining(timer); result.NoDelay = true; return result;
            } catch { result.Close(); throw; }
        }
        void WriteCommand(NetworkStream target, string command, Stopwatch timer)
        {
            byte[] bytes = utf8.GetBytes(command + "\0");
            target.WriteTimeout = Remaining(timer); target.Write(bytes, 0, bytes.Length); Remaining(timer);
        }
        Dictionary<string, object> Request(string command)
        {
            var timer = Stopwatch.StartNew();
            using (TcpClient management = OpenConnection(timer))
            using (NetworkStream stream = management.GetStream())
            using (var output = new MemoryStream()) {
                WriteCommand(stream, command, timer); byte[] buffer = new byte[1024];
                for (;;) {
                    stream.ReadTimeout = Remaining(timer); int count = stream.Read(buffer, 0, buffer.Length); Remaining(timer);
                    if (count == 0) break;
                    if (output.Length + count > MaxResponseBytes) throw new InvalidDataException("Candidate response exceeds 16 KiB.");
                    output.Write(buffer, 0, count);
                }
                var parser = new JavaScriptSerializer { MaxJsonLength = MaxResponseBytes, RecursionLimit = 16 };
                var data = parser.DeserializeObject(utf8.GetString(output.ToArray())) as Dictionary<string, object>;
                if (data == null) throw new InvalidDataException("Candidate response is not a JSON object.");
                if (data.ContainsKey("status"))
                    throw new CandidateApiException(Number(data, "status"), "Candidate API status " + Number(data, "status"));
                return data;
            }
        }
        static string Text(Dictionary<string, object> data, string key)
        { object value; return data.TryGetValue(key, out value) && value is string ? (string)value : null; }
        static uint Number(Dictionary<string, object> data, string key)
        {
            object value; uint number;
            if (!data.TryGetValue(key, out value) || !(value is int || value is long || value is decimal) ||
                !UInt32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.None, CultureInfo.InvariantCulture, out number))
                throw new InvalidDataException("Invalid candidate numeric field: " + key);
            return number;
        }
        sealed class CandidateApiException : IOException
        {
            internal readonly uint Status;
            internal CandidateApiException(uint status, string message) : base(message) { Status = status; }
        }
    }
}
