using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using static BTBattLvMon.DevPropKeys;
using static BTBattLvMon.SetupDiApis;

namespace BtMonEx02
{
    public class BTConnBattMonitor
    {
        public readonly record struct Status(string InstanceId, string FriendlyName, bool IsConnected, int BatteryLevel);
       
        private readonly record struct BTGenDeviceItem(DeviceInfo DeviceInfo, string InstanceId, Guid ContainerId);

        private const string UNNKONWN_FRIENDLY_NAME = "Unknown";
        private static readonly Regex REGEX_GENERIC_DEVICE = new("GENERIC.*DEVICE", RegexOptions.IgnoreCase);

        private static readonly DEVPROPKEY DEVPKEY_Device_IsBTConnected = new ()
        {
            fmtid = new Guid("{83DA6326-97A6-4088-9453-A1923F573B29}"),
            pid = 15
        };

        private static readonly DEVPROPKEY DEVPEY_Device_BTBatteryLevel = new()
        {
            fmtid = new Guid("{104EA319-6EE2-4701-BD47-8DDBF425BBE5}"),
            pid = 2
        };

        private static string GetDeviceInfoFriendlyName(DeviceInfo devInfo)
        {
            try
            {
                var friendlyName = GetDevicePropertyString(devInfo, DEVPKEY_Device_FriendlyName);
                return friendlyName;
            }
            catch
            {
                return UNNKONWN_FRIENDLY_NAME;
            }
        }

        private static Guid GetDeviceInfoContainerId(DeviceInfo devInfo)
        {
            try
            {
                var containerId = GetDevicePropertyGuid(devInfo, DEVPKEY_Device_ContainerId);
                return containerId;
            }
            catch
            {
                return new Guid();
            }
        }

        private static bool IsDeviceBTGeneric(DeviceInfo devInfo)
        {
            try
            {
                var compatibleIds = GetDevicePropertyStringArray(devInfo, DEVPKEY_Device_CompatibleIds);
                foreach (var compatibleId in compatibleIds)
                {
                    var m = REGEX_GENERIC_DEVICE.Match(compatibleId);
                    if (m.Success)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDeviceInfoIsBTConnected(DeviceInfo devInfo, out bool isBTConnected)
        {
            isBTConnected = false;
            try
            {
                isBTConnected = GetDevicePropertyBoolean(devInfo, DEVPKEY_Device_IsBTConnected);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDeviceInfoBatteryLevel(DeviceInfo devInfo, out int batteryLevel)
        {
            batteryLevel = 0;
            try
            {
                batteryLevel = GetDevicePropertyByte(devInfo, DEVPEY_Device_BTBatteryLevel);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Status[] GetStatus()
        {
            using var snapshot = GetDeviceSnapshot();

            var dictContainer = new Dictionary<Guid, List<DeviceInfo>>();
            var listBTGenDev = new List<BTGenDeviceItem>();
            foreach (var devInfo in DeviceInfos(snapshot))
            {
                var instanceId = GetDeviceInstanceId(devInfo);
                if (!instanceId.StartsWith("BT"))
                {
                    continue;
                }

                var containerId = GetDeviceInfoContainerId(devInfo);
                if (!dictContainer.ContainsKey(containerId))
                {
                    dictContainer[containerId] = new();
                }
                dictContainer[containerId].Add(devInfo);

                var isBTGenDev = IsDeviceBTGeneric(devInfo);
                if (isBTGenDev)
                {
                    listBTGenDev.Add(new BTGenDeviceItem(devInfo, instanceId, containerId));
                }
            }

            var listStatus = new List<Status>();
            foreach (var genDevItem in listBTGenDev)
            {
                var friendlyName = GetDeviceInfoFriendlyName(genDevItem.DeviceInfo);
                var isBTConnected = false;
                if (!TryGetDeviceInfoIsBTConnected(genDevItem.DeviceInfo, out isBTConnected))
                {
                    isBTConnected = false;
                }
                var batteryLevel = 0;
                if (isBTConnected)
                {
                    var container = dictContainer[genDevItem.ContainerId];
                    foreach (var devInfo in container)
                    {
                        if (TryGetDeviceInfoBatteryLevel(devInfo, out batteryLevel))
                        {
                            break;
                        }
                    }
                }
                var status = new Status(genDevItem.InstanceId, friendlyName, isBTConnected, batteryLevel);
                listStatus.Add(status);
            }

            return listStatus.ToArray();
        }

        public Task<Status[]> GetStatusesAsync()
        {
            return Task.Run<Status[]>(() => GetStatus());
        }
    }
}
