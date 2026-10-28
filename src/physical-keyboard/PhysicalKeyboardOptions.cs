using System;
using System.IO;

namespace PSKeyboard
{
    // Argument parsing has no GUI, process, device or network side effects.
    internal sealed class PhysicalKeyboardOptions
    {
        internal readonly string BusId;
        internal readonly bool StartRelay;
        internal readonly string StatusFile;

        private PhysicalKeyboardOptions(string busId, bool startRelay, string statusFile)
        {
            BusId = busId;
            StartRelay = startRelay;
            StatusFile = statusFile;
        }

        internal static PhysicalKeyboardOptions Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException("args");
            string busId = "", statusFile = null;
            bool startRelay = false, busSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--start-relay")
                {
                    if (busSet || i + 1 >= args.Length || !ValidBusId(args[i + 1])) throw Usage();
                    busId = args[++i]; startRelay = true; busSet = true;
                }
                else if (args[i] == "--status-file")
                {
                    if (statusFile != null || i + 1 >= args.Length) throw Usage();
                    statusFile = ValidateStatusFile(args[++i]);
                }
                else
                {
                    if (busSet || args[i] == null || args[i].StartsWith("--", StringComparison.Ordinal)) throw Usage();
                    busId = args[i]; busSet = true;
                }
            }
            return new PhysicalKeyboardOptions(busId, startRelay, statusFile);
        }

        private static ArgumentException Usage()
        {
            return new ArgumentException("用法：PSKeyboard-Physical.exe [手把位置]，或 --start-relay <已核對的手把位置>；可另加 --status-file <本機完整 JSON 路徑>。");
        }

        internal static string ValidateStatusFile(string value)
        {
            // Explicit local drive paths only; no relative path, UNC share or
            // alternate data stream. Never create an implicit logging location.
            if (String.IsNullOrEmpty(value) || value.Length < 4 ||
                !((value[0] >= 'A' && value[0] <= 'Z') || (value[0] >= 'a' && value[0] <= 'z')) ||
                value[1] != ':' || (value[2] != '\\' && value[2] != '/') || value.IndexOf(':', 2) >= 0)
                throw Usage();
            string full = Path.GetFullPath(value);
            if (!String.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase)) throw Usage();
            return full;
        }

        private static bool ValidBusId(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 31) return false;
            // usbipd-win uses numeric bus-port locations. Keep command-like,
            // whitespace and non-ASCII arguments out of automatic startup.
            for (int i = 0; i < value.Length; i++)
                if (!((value[i] >= '0' && value[i] <= '9') || value[i] == '-' || value[i] == '.')) return false;
            return value[0] >= '0' && value[0] <= '9' && value[value.Length - 1] >= '0' && value[value.Length - 1] <= '9';
        }
    }
}
