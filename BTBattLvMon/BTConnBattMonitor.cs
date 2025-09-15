using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using static BTBattLvMon.DevPropKeys;
using static BTBattLvMon.SetupDiApis;

namespace BTBattLvMon   
{
    public class BTConnBattMonitor
    {
        private readonly record struct BTGenDeviceItem(DeviceInfo DeviceInfo, string InstanceId, Guid ContainerId);

        private const string UNKONWN_FRIENDLY_NAME = "Unknown";
        private static readonly Regex REGEX_GENERIC_DEVICE = new("GENERIC.*DEVICE", RegexOptions.IgnoreCase);

        private static readonly DEVPROPKEY DEVPKEY_Device_IsBTConnected = new()
        {
            fmtid = new Guid("{83DA6326-97A6-4088-9453-A1923F573B29}"),
            pid = 15
        };

        private static readonly DEVPROPKEY DEVPKEY_Device_BTBatteryLevel = new()
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
                return UNKONWN_FRIENDLY_NAME;
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
                batteryLevel = GetDevicePropertyByte(devInfo, DEVPKEY_Device_BTBatteryLevel);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private CancellationTokenSource _cts = new();
        private Task _task = Task.CompletedTask;

        public IReadOnlyCollection<BattStatus> GetStatuses()
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

            var listStatus = new List<BattStatus>();
            foreach (var genDevItem in listBTGenDev)
            {
                if (!TryGetDeviceInfoIsBTConnected(genDevItem.DeviceInfo, out bool isBTConnected) || !isBTConnected)
                {
                    continue;
                }
                var friendlyName = GetDeviceInfoFriendlyName(genDevItem.DeviceInfo);
                var batteryLevel = 0;
                var container = dictContainer[genDevItem.ContainerId];
                foreach (var devInfo in container)
                {
                    if (TryGetDeviceInfoBatteryLevel(devInfo, out batteryLevel))
                    {
                        break;
                    }
                }
                var status = new BattStatus(genDevItem.InstanceId, friendlyName, batteryLevel);
                listStatus.Add(status);
            }

            return listStatus.AsReadOnly();
        }
        private static bool StatusesEquals(IReadOnlyCollection<BattStatus> a, IReadOnlyCollection<BattStatus> b)
        {
            if (a.Count != b.Count) return false;
            var enumA = a.GetEnumerator();
            var enumB = b.GetEnumerator();
            while (enumA.MoveNext() && enumB.MoveNext())
            {
                if (!enumA.Current.Equals(enumB.Current)) return false;
            }
            return true;
        }

        private async Task WatchAsync(Action<IReadOnlyCollection<BattStatus>> onChanged, int intervalMs, CancellationToken token)
        {
            try
            {
                var lastStatuses = Array.Empty<BattStatus>() as IReadOnlyCollection<BattStatus>;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var statuses = this.GetStatuses();
                        if (!StatusesEquals(lastStatuses, statuses))
                        {
                            lastStatuses = statuses;
                            onChanged(statuses);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                    await Task.Delay(intervalMs, token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            Debug.WriteLine("BTConnBattMonitor.WatchAsync: canceled");
        }

        public async void StartMonitor(Action<IReadOnlyCollection<BattStatus>> onChange, int intervalMs = 5000)
        {
            await this.StopMonitorAsync();
            _cts = new CancellationTokenSource();
            _task = Task.Run(async () =>
            {
                await this.WatchAsync(onChange, intervalMs, _cts.Token);
            }, _cts.Token);
        }

        public async Task StopMonitorAsync()
        {
            _cts.Cancel();
            try
            {
                await _task;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _task = Task.CompletedTask;
        }
    }
}
