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

namespace PSKeyboard
{
    // Local VIIPER v0.9 transport only. Creating a device here does not prove
    // Windows attached it or Remote Play accepted it. The caller runs a server
    // with auto-attach disabled and handles any explicit USBIP attachment.
    public sealed class ViiperTransport : IDisposable
    {
        public const int DefaultPort = 33242;
        public const int FrameSize = 31;
        const int MaxResponseBytes = 16384;
        readonly object gate = new object();
        readonly int port, timeoutMs, settleMs;
        readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        TcpClient client;
        NetworkStream stream;
        uint busId;
        string deviceId;
        bool disposed;
        volatile bool connected;
        volatile string lastFailure;
        volatile string serverVersion;
        long feedbackPackets;

        public ViiperTransport(int port) : this(port, 1500, 200) { }
        internal ViiperTransport(int port, int timeoutMs, int settleMs)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");
            if (timeoutMs < 50 || timeoutMs > 10000) throw new ArgumentOutOfRangeException("timeoutMs");
            if (settleMs < 0 || settleMs > timeoutMs) throw new ArgumentOutOfRangeException("settleMs");
            this.port = port; this.timeoutMs = timeoutMs; this.settleMs = settleMs;
        }

        public bool IsConnected { get { return connected; } }
        public string LastFailure { get { return lastFailure; } }
        public string ServerVersion { get { return serverVersion; } }
        public long FeedbackPackets { get { return Interlocked.Read(ref feedbackPackets); } }
        public uint BusId { get { lock (gate) { return busId; } } }
        public string DeviceId { get { lock (gate) { return deviceId; } } }
        public string UsbBusId
        {
            get { lock (gate) { return busId == 0 || deviceId == null ? null :
                busId.ToString(CultureInfo.InvariantCulture) + "-" + deviceId; } }
        }

        public static byte[] CreateNeutralFrame()
        {
            byte[] result = new byte[FrameSize];
            result[29] = 0x00; result[30] = 0xE0; // signed AccelZ = -8192
            return result;
        }

        public void Connect()
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("ViiperTransport");
                if (connected) return;
                if (busId != 0)
                {
                    DisconnectCore(false);
                    if (busId != 0) throw new IOException("Previous VIIPER bus cleanup is not confirmed: " + lastFailure);
                }
                lastFailure = null;
                try
                {
                    Dictionary<string, object> ping = Request("ping");
                    if (Text(ping, "server") != "VIIPER") throw new InvalidDataException("The local service is not VIIPER.");
                    serverVersion = Text(ping, "version");
                    if (String.IsNullOrWhiteSpace(serverVersion)) throw new InvalidDataException("VIIPER version is missing.");
                    Dictionary<string, object> created = Request("bus/create");
                    busId = Number(created, "busId");
                    if (busId == 0) throw new InvalidDataException("VIIPER returned an invalid bus ID.");
                    Dictionary<string, object> added = Request("bus/" + BusNumber() + "/add {\"type\":\"dualshock4\"}");
                    if (Number(added, "busId") != busId || Text(added, "type") != "dualshock4")
                        throw new InvalidDataException("VIIPER returned a different bus or device type.");
                    string id = Text(added, "devId");
                    uint numericId;
                    if (!UInt32.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out numericId) || numericId == 0)
                        throw new InvalidDataException("VIIPER returned an invalid device ID.");
                    deviceId = numericId.ToString(CultureInfo.InvariantCulture);
                    client = OpenConnection(); stream = client.GetStream();
                    WriteCommand(stream, "bus/" + BusNumber() + "/" + deviceId);
                    // The protocol has no success ACK. Reject immediate errors/EOF,
                    // and keep handshake bytes separate from the first input frame.
                    if (client.Client.Poll(settleMs * 1000, SelectMode.SelectRead))
                        ReadFeedbackPacket(stream, true);
                    WriteFrame(stream, CreateNeutralFrame());
                    connected = true;
                    TcpClient activeClient = client;
                    NetworkStream activeStream = stream;
                    Thread reader = new Thread(delegate() { ReadFeedback(activeClient, activeStream); });
                    reader.IsBackground = true; reader.Name = "VIIPER feedback"; reader.Start();
                }
                catch (Exception ex)
                {
                    lastFailure = ex.Message;
                    DisconnectCore(false);
                    throw new IOException("Could not establish the local VIIPER device stream: " + lastFailure, ex);
                }
            }
        }

        public void Send(byte[] frame31)
        {
            if (frame31 == null) throw new ArgumentNullException("frame31");
            if (frame31.Length != FrameSize) throw new ArgumentException("Expected exactly 31 DS4 state bytes.", "frame31");
            byte[] stableFrame = (byte[])frame31.Clone();
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("ViiperTransport");
                if (!connected || stream == null) throw new IOException("VIIPER stream is disconnected. " + lastFailure);
                try { WriteFrame(stream, stableFrame); }
                catch (Exception ex)
                {
                    lastFailure = "VIIPER input send failed: " + ex.Message;
                    DisconnectCore(false);
                    throw new IOException(lastFailure, ex);
                }
            }
        }

        public void Disconnect() { lock (gate) { DisconnectCore(true); } }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                DisconnectCore(true); disposed = true;
            }
        }

        void DisconnectCore(bool sendNeutral)
        {
            if (sendNeutral && connected && stream != null)
            {
                try { WriteFrame(stream, CreateNeutralFrame()); }
                catch (Exception ex) { RememberFailure("Neutral state delivery was not confirmed: " + ex.Message); }
            }
            connected = false;
            TcpClient closing = client; client = null; stream = null;
            if (closing != null) closing.Close();
            if (busId == 0) { deviceId = null; return; }
            try
            {
                Dictionary<string, object> removed = Request("bus/remove " + BusNumber());
                if (Number(removed, "busId") != busId) throw new InvalidDataException("VIIPER returned a different removed bus ID.");
                busId = 0; deviceId = null;
            }
            catch (ViiperApiException ex)
            {
                if (ex.Status == 404) { busId = 0; deviceId = null; }
                else RememberFailure("Own bus cleanup failed: " + ex.Message);
            }
            catch (Exception ex) { RememberFailure("Own bus cleanup failed: " + ex.Message); }
        }

        void RememberFailure(string detail)
        {
            lastFailure = String.IsNullOrEmpty(lastFailure) ? detail : lastFailure + " | " + detail;
        }
        string BusNumber() { return busId.ToString(CultureInfo.InvariantCulture); }

        TcpClient OpenConnection()
        {
            TcpClient result = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                IAsyncResult operation = result.BeginConnect(IPAddress.Loopback, port, null, null);
                using (WaitHandle done = operation.AsyncWaitHandle)
                {
                    if (!done.WaitOne(timeoutMs)) throw new TimeoutException("Local VIIPER connection timed out.");
                    result.EndConnect(operation);
                }
                result.NoDelay = true; result.SendTimeout = timeoutMs; result.ReceiveTimeout = timeoutMs;
                return result;
            }
            catch { result.Close(); throw; }
        }

        void WriteCommand(NetworkStream target, string command)
        {
            byte[] bytes = utf8.GetBytes(command + "\0");
            target.WriteTimeout = timeoutMs; target.Write(bytes, 0, bytes.Length);
        }
        void WriteFrame(NetworkStream target, byte[] frame)
        {
            target.WriteTimeout = timeoutMs; target.Write(frame, 0, frame.Length);
        }

        Dictionary<string, object> Request(string command)
        {
            using (TcpClient management = OpenConnection())
            using (NetworkStream target = management.GetStream())
            {
                WriteCommand(target, command);
                return ParseResponse(ReadUntilEof(target, null));
            }
        }
        byte[] ReadUntilEof(NetworkStream target, byte[] prefix)
        {
            using (MemoryStream output = new MemoryStream())
            {
                if (prefix != null) output.Write(prefix, 0, prefix.Length);
                Stopwatch deadline = Stopwatch.StartNew(); byte[] buffer = new byte[1024];
                while (true)
                {
                    int remaining = timeoutMs - (int)deadline.ElapsedMilliseconds;
                    if (remaining <= 0) throw new TimeoutException("VIIPER response did not finish before the deadline.");
                    target.ReadTimeout = remaining;
                    int count = target.Read(buffer, 0, buffer.Length);
                    if (count == 0) return output.ToArray();
                    if (output.Length + count > MaxResponseBytes) throw new InvalidDataException("VIIPER response exceeds the size limit.");
                    output.Write(buffer, 0, count);
                }
            }
        }
        Dictionary<string, object> ParseResponse(byte[] bytes)
        {
            string response = utf8.GetString(bytes).Trim();
            if (response.Length == 0) throw new InvalidDataException("VIIPER returned an empty response.");
            JavaScriptSerializer json = new JavaScriptSerializer(); json.MaxJsonLength = MaxResponseBytes;
            Dictionary<string, object> data = json.DeserializeObject(response) as Dictionary<string, object>;
            if (data == null) throw new InvalidDataException("VIIPER response is not a JSON object.");
            if (data.ContainsKey("status"))
            {
                uint status = Number(data, "status");
                throw new ViiperApiException((int)status, "VIIPER " + status.ToString(CultureInfo.InvariantCulture) +
                    " " + Text(data, "title") + ": " + Text(data, "detail"));
            }
            return data;
        }
        static string Text(Dictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) && value is string ? (string)value : null;
        }
        static uint Number(Dictionary<string, object> data, string key)
        {
            object value; uint number;
            if (!data.TryGetValue(key, out value) || value == null ||
                !(value is int || value is long || value is decimal) ||
                !UInt32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.None,
                    CultureInfo.InvariantCulture, out number)) throw new InvalidDataException("Invalid VIIPER numeric field: " + key);
            return number;
        }

        void ReadFeedbackPacket(NetworkStream target, bool initial)
        {
            byte[] packet = new byte[8]; int offset = 0; Stopwatch partial = null;
            while (offset < packet.Length)
            {
                target.ReadTimeout = offset == 0 && !initial ? Math.Min(timeoutMs, 500) : timeoutMs;
                try
                {
                    int count = target.Read(packet, offset, packet.Length - offset);
                    if (count == 0) throw new EndOfStreamException(offset == 0 ? "VIIPER closed the device stream." : "VIIPER truncated a feedback packet.");
                    offset += count;
                    if (partial == null) partial = Stopwatch.StartNew();
                    if (packet[0] == (byte)'{')
                    {
                        byte[] prefix = new byte[offset]; Buffer.BlockCopy(packet, 0, prefix, 0, offset);
                        ParseResponse(ReadUntilEof(target, prefix));
                        throw new InvalidDataException("Unexpected JSON on the VIIPER feedback stream.");
                    }
                    if ((packet[0] & ~7) != 0) throw new InvalidDataException("Invalid VIIPER feedback flags or authentication response.");
                    if (partial.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("VIIPER feedback packet timed out.");
                }
                catch (IOException ex)
                {
                    SocketException socket = ex.InnerException as SocketException;
                    if (offset == 0 && !initial && socket != null && socket.SocketErrorCode == SocketError.TimedOut) continue;
                    throw;
                }
            }
            Interlocked.Increment(ref feedbackPackets);
        }
        void ReadFeedback(TcpClient activeClient, NetworkStream activeStream)
        {
            try { while (true) ReadFeedbackPacket(activeStream, false); }
            catch (Exception ex)
            {
                lock (gate)
                {
                    if (!Object.ReferenceEquals(client, activeClient)) return;
                    lastFailure = "VIIPER feedback disconnected: " + ex.Message;
                    DisconnectCore(false);
                }
            }
        }
    }

    internal sealed class ViiperApiException : IOException
    {
        public int Status { get; private set; }
        public ViiperApiException(int status, string message) : base(message) { Status = status; }
    }
}
