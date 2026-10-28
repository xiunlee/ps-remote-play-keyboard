using System;

namespace PSKeyboard
{
    // Argument parsing has no GUI, process, device or network side effects.
    internal sealed class PhysicalKeyboardOptions
    {
        internal readonly string BusId;
        internal readonly bool StartRelay;

        private PhysicalKeyboardOptions(string busId, bool startRelay)
        {
            BusId = busId;
            StartRelay = startRelay;
        }

        internal static PhysicalKeyboardOptions Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException("args");
            if (args.Length == 0) return new PhysicalKeyboardOptions("", false);
            if (args.Length == 1 && args[0] != null && !args[0].StartsWith("--", StringComparison.Ordinal))
                return new PhysicalKeyboardOptions(args[0], false);
            if (args.Length != 2 || args[0] != "--start-relay" || !ValidBusId(args[1]))
                throw new ArgumentException("用法：PSKeyboard-Physical.exe [手把位置]，或 --start-relay <已核對的手把位置>。");
            return new PhysicalKeyboardOptions(args[1], true);
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
