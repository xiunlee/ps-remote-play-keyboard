using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PSKeyboard
{
    public enum RapidFireMode { Off = 0, Hold = 1, Toggle = 2 }

    // Immutable one-key-per-action profile. Action order is the native merge ABI.
    public sealed class BindingProfile
    {
        public const int ActionCount = 22;
        public const int MinRapidFireIntervalMilliseconds = 20;
        public const int MaxRapidFireIntervalMilliseconds = 5000;
        public const int DefaultRapidFireIntervalMilliseconds = 100;
        private const int SchemaVersion = 2, MaxFileBytes = 16384;
        private readonly int[] keys;
        private readonly int[] rapidFireModes, intervalMilliseconds;
        private readonly uint[] actionsByKey;

        private BindingProfile(int[] source) : this(source, new int[ActionCount], DefaultIntervals()) { }

        private static int[] DefaultIntervals()
        {
            int[] result = new int[ActionCount];
            for (int i = 0; i < result.Length; ++i) result[i] = DefaultRapidFireIntervalMilliseconds;
            return result;
        }

        private BindingProfile(int[] source, int[] modes, int[] intervals)
        {
            if (source == null || source.Length != ActionCount)
                throw new ArgumentException("Expected exactly 22 action bindings.", "source");
            if (modes == null || modes.Length != ActionCount || intervals == null || intervals.Length != ActionCount)
                throw new ArgumentException("Expected exactly 22 rapid-fire settings.");
            keys = (int[])source.Clone();
            rapidFireModes = (int[])modes.Clone();
            intervalMilliseconds = (int[])intervals.Clone();
            actionsByKey = new uint[256];
            for (int action = 0; action < keys.Length; ++action)
            {
                ValidateRapidFire((RapidFireMode)rapidFireModes[action], intervalMilliseconds[action]);
                int key = keys[action];
                if (!IsAllowedKey(key)) throw new ArgumentException("Unsupported or reserved key.", "source");
                if (key == 0) continue;
                if (actionsByKey[key] != 0) throw new ArgumentException("Duplicate key binding.", "source");
                actionsByKey[key] = 1u << action;
            }
        }

        public static BindingProfile Default()
        {
            return new BindingProfile(new int[] {
                0x57, 0x53, 0x41, 0x44, 0x24, 0x23, 0x2D, 0x21,
                0x28, 0x27, 0x25, 0x26, 0x45, 0x2E, 0x51, 0x22,
                0x09, 0x0D, 0x46, 0x20, 0x70, 0x52
            });
        }

        public static bool IsAllowedKey(int virtualKey)
        {
            if (virtualKey == 0) return true; // Unassigned action.
            if (virtualKey >= 0x30 && virtualKey <= 0x39) return true;
            if (virtualKey >= 0x41 && virtualKey <= 0x5A) return true;
            if (virtualKey >= 0x60 && virtualKey <= 0x6F) return true; // Numpad.
            if (virtualKey >= 0x70 && virtualKey <= 0x7B) return virtualKey != 0x77;
            if (virtualKey >= 0xBA && virtualKey <= 0xC0) return true;
            if (virtualKey >= 0xDB && virtualKey <= 0xDF) return true;
            switch (virtualKey)
            {
                case 0x08: case 0x09: case 0x0D: case 0x1B: case 0x20:
                case 0x21: case 0x22: case 0x23: case 0x24: case 0x25:
                case 0x26: case 0x27: case 0x28: case 0x2D: case 0x2E:
                case 0xE2:
                    return true;
                default:
                    return false;
            }
        }

        public uint ActionForKey(int virtualKey)
        {
            return virtualKey >= 0 && virtualKey < actionsByKey.Length ? actionsByKey[virtualKey] : 0;
        }

        public int KeyForAction(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            return keys[actionIndex];
        }

        public RapidFireMode RapidFireModeForAction(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            return (RapidFireMode)rapidFireModes[actionIndex];
        }

        public int IntervalMillisecondsForAction(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            return intervalMilliseconds[actionIndex];
        }

        private static void ValidateRapidFire(RapidFireMode mode, int interval)
        {
            if (mode < RapidFireMode.Off || mode > RapidFireMode.Toggle)
                throw new ArgumentOutOfRangeException("mode");
            if (interval < MinRapidFireIntervalMilliseconds || interval > MaxRapidFireIntervalMilliseconds)
                throw new ArgumentOutOfRangeException("intervalMilliseconds");
        }

        public BindingProfile WithRapidFire(int actionIndex, RapidFireMode mode, int interval)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            ValidateRapidFire(mode, interval);
            int[] changedModes = (int[])rapidFireModes.Clone(), changedIntervals = (int[])intervalMilliseconds.Clone();
            changedModes[actionIndex] = (int)mode; changedIntervals[actionIndex] = interval;
            return new BindingProfile(keys, changedModes, changedIntervals);
        }

        public BindingProfile WithBinding(int actionIndex, int virtualKey)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            if (!IsAllowedKey(virtualKey)) throw new ArgumentException("Unsupported or reserved key.", "virtualKey");
            if (virtualKey != 0 && actionsByKey[virtualKey] != 0 && keys[actionIndex] != virtualKey)
                throw new ArgumentException("Key is already assigned to another action.", "virtualKey");
            int[] changed = (int[])keys.Clone();
            changed[actionIndex] = virtualKey;
            return new BindingProfile(changed, rapidFireModes, intervalMilliseconds);
        }

        public void Save(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", "path");
            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".bindings-" + Guid.NewGuid().ToString("N") + ".tmp");
            Dictionary<string, object> data = new Dictionary<string, object> {
                { "version", SchemaVersion }, { "keys", (int[])keys.Clone() },
                { "rapidFireModes", (int[])rapidFireModes.Clone() },
                { "intervalMilliseconds", (int[])intervalMilliseconds.Clone() }
            };
            try
            {
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(data), new UTF8Encoding(false));
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

        public static BindingProfile Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", "path");
            string json;
            try
            {
                if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("Profile is too large.");
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (FileNotFoundException) { return Default(); }
            catch (DirectoryNotFoundException) { return Default(); }
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaxFileBytes, RecursionLimit = 8 };
                Dictionary<string, object> data = serializer.DeserializeObject(json) as Dictionary<string, object>;
                object version;
                if (data == null || !data.TryGetValue("version", out version) || !(version is int))
                    throw new InvalidDataException("Unsupported profile schema.");
                if ((int)version == 1)
                {
                    if (data.Count != 2) throw new InvalidDataException("Unsupported legacy profile schema.");
                    return new BindingProfile(ReadIntegers(data, "keys"));
                }
                if ((int)version != SchemaVersion || data.Count != 4)
                    throw new InvalidDataException("Unsupported profile schema.");
                return new BindingProfile(ReadIntegers(data, "keys"), ReadIntegers(data, "rapidFireModes"),
                    ReadIntegers(data, "intervalMilliseconds"));
            }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid keyboard profile.", ex); }
            catch (InvalidOperationException ex) { throw new InvalidDataException("Invalid keyboard profile.", ex); }
        }

        private static int[] ReadIntegers(Dictionary<string, object> data, string name)
        {
            object values;
            if (!data.TryGetValue(name, out values)) throw new InvalidDataException("Missing profile setting: " + name);
            object[] array = values as object[];
            if (array == null || array.Length != ActionCount) throw new InvalidDataException("Invalid setting count: " + name);
            int[] result = new int[ActionCount];
            for (int i = 0; i < result.Length; ++i)
            {
                if (!(array[i] is int)) throw new InvalidDataException("Settings must be integer values: " + name);
                result[i] = (int)array[i];
            }
            return result;
        }
    }
}
