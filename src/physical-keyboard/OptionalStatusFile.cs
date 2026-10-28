using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace PSKeyboard
{
    internal sealed class RelayStatusSnapshot
    {
        internal int Pid;
        internal bool Enabled, Attached, EndpointVerified;
        internal long InputReports, MergedReports;
        internal string Failure;

        internal string ToJson()
        {
            string failure = SanitizeFailure(Failure);
            return "{\"pid\":" + Pid.ToString(CultureInfo.InvariantCulture) +
                ",\"enabled\":" + (Enabled ? "true" : "false") +
                ",\"attached\":" + (Attached ? "true" : "false") +
                ",\"endpointVerified\":" + (EndpointVerified ? "true" : "false") +
                ",\"inputReports\":" + InputReports.ToString(CultureInfo.InvariantCulture) +
                ",\"mergedReports\":" + MergedReports.ToString(CultureInfo.InvariantCulture) +
                ",\"failure\":" + (failure == null ? "null" : "\"" + failure + "\"") + "}\n";
        }

        private static string SanitizeFailure(string failure)
        {
            if (failure == null) return null;
            switch (failure)
            {
                case "USB/IP protocol validation failed": return "protocol_validation_failed";
                case "USB/IP peer disconnected": return "peer_disconnected";
                case "USB/IP connection ended": return "connection_ended";
                case "USB/IP relay stopped": return "relay_stopped";
                case "Keyboard input setup failed": return "keyboard_setup_failed";
                case "Relay startup failed": return "relay_startup_failed";
                default: return "relay_error";
            }
        }
    }

    // Optional diagnostics run on their own worker: disk I/O never delays the
    // UI thread, keyboard hook, or physical USB report forwarding.
    internal sealed class OptionalStatusFile : IDisposable
    {
        private readonly string path;
        private readonly Func<RelayStatusSnapshot> snapshot;
        private readonly ManualResetEvent stopping = new ManualResetEvent(false);
        private Thread worker;
        private bool disposed;

        internal OptionalStatusFile(string path, Func<RelayStatusSnapshot> snapshot)
        {
            this.path = path == null ? null : PhysicalKeyboardOptions.ValidateStatusFile(path);
            this.snapshot = snapshot;
        }

        internal void Start()
        {
            if (disposed || worker != null) throw new InvalidOperationException("Status writer cannot restart.");
            if (path == null) return;
            worker = new Thread(Run) { IsBackground = true, Name = "PS keyboard optional status" };
            worker.Start();
        }

        private void Run()
        {
            do { TryWrite(); } while (!stopping.WaitOne(500));
            TryWrite();
        }

        private void TryWrite()
        {
            try { WriteAtomic(path, snapshot().ToJson()); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }

        internal static void WriteAtomic(string destination, string json)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; stopping.Set();
            if (worker == null || worker.Join(2500)) stopping.Dispose();
            // A stalled filesystem may outlive the bounded join. Retain its
            // event until process exit rather than disposing a live worker's wait.
        }
    }
}
