using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static BTBattLvMon.DevPropKeys;
using static BTBattLvMon.DevPropTypes;

namespace BTBattLvMon
{
    public class SetupDiApis
    {
        public record struct InstanceId(string Value)
        {
            public override string ToString()
            {
                return Value;
            }
        }

        public class SetupDiApiException : System.Exception
        {
            public SetupDiApiException(string msg) : base(msg)
            {

            }
            public SetupDiApiException(string msg, System.Exception innerException) : base(msg, innerException)
            {

            }
        }

        public class SetupDiApiNotFoundException : SetupDiApiException
        {
            public SetupDiApiNotFoundException(string msg) : base(msg) { }
        }

        public class SetupDiApiInnerApiFailedException : SetupDiApiException
        {
            public SetupDiApiInnerApiFailedException(string msg) : base(msg) { }
        }

        public class SetupDiApiInvalidDataException : SetupDiApiException
        {
            public SetupDiApiInvalidDataException(string msg) : base(msg) { }
        }

        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_ALLCLASSES = 0x00000004;

        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_NOT_FOUND = 1168;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern DeviceInfoSnapshot SetupDiGetClassDevs(
            IntPtr ClassGuid,
            string? Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet,
            in SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId,
            int DeviceInstanceIdSize,
            out int RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyKeys(
            IntPtr DeviceInfoSet,
            in SP_DEVINFO_DATA DeviceInfoData,
            [Out] DEVPROPKEY[]? PropertyKeyArray,
            uint PropertyKeyCount,
            out uint RequiredPropertyKeyCount,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyW(
            IntPtr DeviceInfoSet,
            in SP_DEVINFO_DATA DeviceInfoData,
            in DevPropKeys.DEVPROPKEY PropertyKey,
            out uint PropertyType,
            byte[]? PropertyBuffer,
            int PropertyBufferSize,
            out int RequiredSize,
            int Flags);

        public sealed class DeviceInfoSnapshot : SafeHandle
        {
            public DeviceInfoSnapshot() : base(IntPtr.Zero, true) { }

            public override bool IsInvalid => this.handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                return SetupDiDestroyDeviceInfoList(this.handle);
            }
        }

        public class DeviceInfo
        {
            public readonly IntPtr DevInfoSet;
            public readonly SP_DEVINFO_DATA DevInfoData;

            public DeviceInfo(IntPtr devInfoSet, SP_DEVINFO_DATA devInfoData)
            {
                this.DevInfoSet = devInfoSet;
                this.DevInfoData = devInfoData;
            }
        }
        public static DeviceInfoSnapshot GetDeviceSnapshot()
        {
            var snapshot = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            return snapshot;
        }
        public static IEnumerable<DeviceInfo> DeviceInfos(DeviceInfoSnapshot snapshot)
        {
            uint index = 0;
            while (true)
            {
                var devInfoSet = snapshot.DangerousGetHandle();
                var devInfoData = new SP_DEVINFO_DATA()
                {
                    cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                };
                var r = SetupDiEnumDeviceInfo(devInfoSet, index, ref devInfoData);
                if (!r)
                {
                    break;
                }
                var devInfo = new DeviceInfo(devInfoSet, devInfoData);
                yield return devInfo;
                index++;
            }
        }

        public static string GetDeviceInstanceId(DeviceInfo devInfo)
        {
            var sb = new StringBuilder(1024);
            var success = SetupDiGetDeviceInstanceId(devInfo.DevInfoSet, in devInfo.DevInfoData, sb, sb.Capacity, out _);
            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDeviceInstanceId returned false. Win32Error: {err}");
            }
            return sb.ToString();
        }

        public static bool GetDevicePropertyBoolean(DeviceInfo devInfo, in DevPropKeys.DEVPROPKEY key)
        {
            byte[] buffer = new byte[16];
            uint propertyType;
            int requiredSize;
            bool success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                buffer,
                buffer.Length,
                out requiredSize,
                0);
            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == ERROR_NOT_FOUND)
                {
                    throw new SetupDiApiNotFoundException($"Not found device property. key: \"{key}\"");
                }
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            if (propertyType != DEVPROP_TYPE_BOOLEAN)
            {
                throw new InvalidDataException($"GetDevicePropertyBoolean() returned. propertyType: {propertyType}, requiredSize: {requiredSize}");
            }
            return buffer[0] != 0;
        }
        public static string GetDevicePropertyString(DeviceInfo devInfo, in DevPropKeys.DEVPROPKEY key)
        {
            byte[] buffer = new byte[1024];
            uint propertyType;
            int requiredSize;
            bool success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                buffer,
                buffer.Length,
                out requiredSize,
                0);
            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == ERROR_NOT_FOUND)
                {
                    throw new SetupDiApiNotFoundException($"Not Found. key: \"{key}\"");
                }
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            if (propertyType != DEVPROP_TYPE_STRING)
            {
                throw new InvalidDataException($"GetDevicePropertyBoolean() returned. propertyType: {propertyType}, requiredSize: {requiredSize}");
            }
            var raw =  Encoding.Unicode.GetString(buffer);
            var idxEnd = raw.IndexOf('\0');
            var strResult = (idxEnd >= 0)? raw.Substring(0, idxEnd) : raw;
            return strResult;
        }

        public static Guid GetDevicePropertyGuid(DeviceInfo devInfo, in DevPropKeys.DEVPROPKEY key)
        {
            uint propertyType;
            byte[] buffer = new byte[16]; // GUID は 16バイト
            int requiredSize;

            bool success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                buffer,
                buffer.Length,
                out requiredSize,
                0);

            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == ERROR_NOT_FOUND)
                {
                    throw new SetupDiApiNotFoundException($"Not Found. key: \"{key}\"");
                }
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            if (propertyType != DEVPROP_TYPE_GUID)
            {
                throw new InvalidDataException($"GetDevicePropertyBoolean() returned. propertyType: {propertyType}, requiredSize: {requiredSize}");
            }
            return new Guid(buffer);
        }
        public static Byte GetDevicePropertyByte(DeviceInfo devInfo, in DevPropKeys.DEVPROPKEY key)
        {
            uint propertyType;
            byte[] buffer = new byte[1];
            int requiredSize;

            bool success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                buffer,
                buffer.Length,
                out requiredSize,
                0);

            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == ERROR_NOT_FOUND)
                {
                    throw new SetupDiApiNotFoundException($"Not Found. key: {key}");
                }
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            if (propertyType != DevPropTypes.DEVPROP_TYPE_BYTE)
            {
                throw new InvalidDataException($"SetupDiGetDevicePropertyW() returned. propertyType: {propertyType}, requiredSize: {requiredSize}");
            }
            return buffer[0];
        }

        public static string[] GetDevicePropertyStringArray(DeviceInfo devInfo, in DevPropKeys.DEVPROPKEY key)
        {
            int requiredSize = 0;
            uint propertyType;
            // 必要なバッファサイズを取得
            bool success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                null,
                0,
                out requiredSize,
                0);
            var err = Marshal.GetLastWin32Error();
            if (!success && err != ERROR_INSUFFICIENT_BUFFER)
            {
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            // バッファを確保
            byte[] buffer = new byte[requiredSize];
            // プロパティを取得
            success = SetupDiGetDevicePropertyW(
                devInfo.DevInfoSet,
                in devInfo.DevInfoData,
                in key,
                out propertyType,
                buffer,
                requiredSize,
                out requiredSize,
                0);

            if (!success)
            {
                throw new SetupDiApiInnerApiFailedException($"SetupDiGetDevicePropertyW returned false. Win32Error: {err}");
            }
            if (!DevPropTypes.IsStringList(propertyType))
            {
                throw new InvalidDataException($"SetupDiGetDevicePropertyW returned. propertyType: {propertyType}, requiredSize: {requiredSize}");
            }
            var multiStr = Encoding.Unicode.GetString(buffer);
            var arrayStr = multiStr.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            return arrayStr;
        }
    }
}
