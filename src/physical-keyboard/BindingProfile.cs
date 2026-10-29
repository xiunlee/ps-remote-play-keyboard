using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PSKeyboard
{
    // Immutable one-key-per-action profile. Action order is the native merge ABI.
    public sealed class BindingProfile
    {
        public const int ActionCount = 22;
        private const int SchemaVersion = 1, MaxFileBytes = 16384;
        private readonly int[] keys;
        private readonly uint[] actionsByKey;

        private BindingProfile(int[] source)
        {
            if (source == null || source.Length != ActionCount)
                throw new ArgumentException("Expected exactly 22 action bindings.", "source");
            keys = (int[])source.Clone();
            actionsByKey = new uint[256];
            for (int action = 0; action < keys.Length; ++action)
            {
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
                0x57, 0x53, 0x41, 0x44, 0x26, 0x28, 0x25, 0x27,
                0x4B, 0x4C, 0x4A, 0x49, 0x51, 0x45, 0x55, 0x4F,
                0x08, 0x0D, 0x31, 0x32, 0x50, 0x54
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

        public BindingProfile WithBinding(int actionIndex, int virtualKey)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount) throw new ArgumentOutOfRangeException("actionIndex");
            if (!IsAllowedKey(virtualKey)) throw new ArgumentException("Unsupported or reserved key.", "virtualKey");
            if (virtualKey != 0 && actionsByKey[virtualKey] != 0 && keys[actionIndex] != virtualKey)
                throw new ArgumentException("Key is already assigned to another action.", "virtualKey");
            int[] changed = (int[])keys.Clone();
            changed[actionIndex] = virtualKey;
            return new BindingProfile(changed);
        }

        public void Save(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A profile path is required.", "path");
            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".bindings-" + Guid.NewGuid().ToString("N") + ".tmp");
            Dictionary<string, object> data = new Dictionary<string, object> {
                { "version", SchemaVersion }, { "keys", (int[])keys.Clone() }
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
                object version, values;
                if (data == null || data.Count != 2 || !data.TryGetValue("version", out version) ||
                    !(version is int) || (int)version != SchemaVersion || !data.TryGetValue("keys", out values))
                    throw new InvalidDataException("Unsupported profile schema.");
                object[] array = values as object[];
                if (array == null || array.Length != ActionCount) throw new InvalidDataException("Invalid binding count.");
                int[] loaded = new int[ActionCount];
                for (int i = 0; i < loaded.Length; ++i)
                {
                    if (!(array[i] is int)) throw new InvalidDataException("Bindings must be integer virtual-key codes.");
                    loaded[i] = (int)array[i];
                }
                return new BindingProfile(loaded);
            }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid keyboard profile.", ex); }
            catch (InvalidOperationException ex) { throw new InvalidDataException("Invalid keyboard profile.", ex); }
        }
    }
}
