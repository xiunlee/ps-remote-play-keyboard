// Read-only metadata probe. Does not consume input reports, submit output or
// feature reports, access PSN data, or inspect the Remote Play process.
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Ds4Research
{
    internal static class HidCapsProbe
    {
        private const string TargetId = "vid_054c&pid_05c4";
        private const uint ShareReadAndWrite = 0x00000003;
        private const uint OpenExisting = 3;
        private const int HidpStatusSuccess = 0x00110000;

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_Interface_List_SizeW(out uint length,
            ref Guid interfaceClass, string deviceId, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_Interface_ListW(ref Guid interfaceClass,
            string deviceId, [Out] char[] buffer, uint bufferLength, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct DevicePropertyKey { public Guid FormatId; public uint PropertyId; }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_Interface_PropertyW(string path,
            ref DevicePropertyKey key, out uint propertyType, byte[] propertyBuffer,
            ref uint propertyBufferSize, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(out uint node, string deviceId, uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint parent, uint node, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_IDW(uint node, StringBuilder buffer, int capacity, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint desiredAccess,
            uint shareMode, IntPtr securityAttributes, uint creationDisposition,
            uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);

        [DllImport("hid.dll")]
        private static extern bool HidD_FreePreparsedData(IntPtr data);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HidCaps caps);

        // HIDP_CAPS contains only fixed-width USHORT fields; Reserved is 17 USHORTs.
        [StructLayout(LayoutKind.Sequential)]
        private struct HidCaps
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength,
                FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps,
                NumberInputValueCaps, NumberInputDataIndices, NumberOutputButtonCaps,
                NumberOutputValueCaps, NumberOutputDataIndices, NumberFeatureButtonCaps,
                NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        // Match by an actual USB ancestor, rather than just the vendor/product
        // substring in a HID path (which could also match Bluetooth devices).
        private static bool HasTargetUsbAncestor(string path)
        {
            DevicePropertyKey key = new DevicePropertyKey {
                FormatId = new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), PropertyId = 256
            };
            byte[] buffer = new byte[4096]; uint size = (uint)buffer.Length, type;
            if (CM_Get_Device_Interface_PropertyW(path, ref key, out type, buffer, ref size, 0) != 0 ||
                type != 0x12 || size < 2 || size > buffer.Length || size % 2 != 0) return false;
            string deviceId = Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
            uint node;
            if (CM_Locate_DevNodeW(out node, deviceId, 0) != 0) return false;
            for (int depth = 0; depth < 16; depth++)
            {
                StringBuilder ancestor = new StringBuilder(512);
                if (CM_Get_Device_IDW(node, ancestor, ancestor.Capacity, 0) != 0) return false;
                if (ancestor.ToString().StartsWith("USB\\VID_054C&PID_05C4", StringComparison.OrdinalIgnoreCase))
                    return true;
                uint parent;
                if (CM_Get_Parent(out parent, node, 0) != 0) return false;
                node = parent;
            }
            return false;
        }

        private static int Main()
        {
            try
            {
                // HID interface class, per Microsoft HidD_GetHidGuid documentation.
                Guid hidInterface = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
                uint length;
                int status = CM_Get_Device_Interface_List_SizeW(out length, ref hidInterface, null, 0);
                if (status != 0) throw new InvalidOperationException("HID list size failed: " + status);
                char[] names = new char[length];
                status = CM_Get_Device_Interface_ListW(ref hidInterface, null, names, length, 0);
                if (status != 0) throw new InvalidOperationException("HID list failed: " + status);
                int success = 0, matches = 0;
                foreach (string path in new string(names).Split('\0'))
                {
                    if (path.IndexOf(TargetId, StringComparison.OrdinalIgnoreCase) < 0 || !HasTargetUsbAncestor(path)) continue;
                    // The target is a USB instance only; no serial numbers are printed.
                    matches++;
                    using (SafeFileHandle device = CreateFile(path, 0, ShareReadAndWrite,
                        IntPtr.Zero, OpenExisting, 0, IntPtr.Zero))
                    {
                        if (device.IsInvalid)
                        {
                            Console.Error.WriteLine("Metadata open failed: Win32 " + Marshal.GetLastWin32Error());
                            continue;
                        }
                        IntPtr preparsed;
                        if (!HidD_GetPreparsedData(device, out preparsed))
                        {
                            Console.Error.WriteLine("Preparsed metadata unavailable: Win32 " + Marshal.GetLastWin32Error());
                            continue;
                        }
                        try
                        {
                            HidCaps caps;
                            int result = HidP_GetCaps(preparsed, out caps);
                            if (result != HidpStatusSuccess)
                            {
                                Console.Error.WriteLine("HidP_GetCaps failed: 0x" + result.ToString("X8"));
                                continue;
                            }
                            success++;
                            Console.WriteLine("DS4 HID collection: usage page=0x{0:X4}, usage=0x{1:X4}", caps.UsagePage, caps.Usage);
                            Console.WriteLine("Maximum report length: input={0}, output={1}, feature={2} bytes",
                                caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength);
                            Console.WriteLine("Input capabilities: button={0}, value={1}",
                                caps.NumberInputButtonCaps, caps.NumberInputValueCaps);
                        }
                        finally { HidD_FreePreparsedData(preparsed); }
                    }
                }
                if (matches == 0) { Console.Error.WriteLine("No matching DS4 USB HID collection is currently present."); return 2; }
                if (success != matches) return 1;
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Metadata probe failed: " + exception.Message);
                return 1;
            }
        }
    }
}
