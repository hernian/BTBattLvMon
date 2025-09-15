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

        private const int INTERVAL_MS = 5000; // 5000ms = 5s
        private const int NORMAL_INTERVAL_COUNT = 6; // 6 * 5000ms = 30000ms = 30s
        private const int FAST_SCAN_COUNT = 12;   // 12 * 5000ms = 60000ms = 60s

        private CancellationTokenSource _cts = new();
        private Task _task = Task.CompletedTask;
        private volatile int _intervalCount = 0;
        private volatile int _fastScanCount = 0;

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

        private int DecreaseIntervalCount()
        {
            lock (this)
            {
                if (_intervalCount > 0)
                {
                    _intervalCount--;
                }
                var ret = _intervalCount;
                if (_intervalCount == 0)
                {
                    if (_fastScanCount > 0)
                    {
                        _fastScanCount--;
                        _intervalCount = 1;
                    }
                    else
                    {
                        _intervalCount = NORMAL_INTERVAL_COUNT;
                    }
                }
#if DEBUG
                Debug.WriteLine($"DecreaseIntervalCount. ret: {ret}, _fastScanCount: {_fastScanCount}, _intervalCount: {_intervalCount}");
#endif
                return ret;
            }
        }

        private async Task WatchAsync(Action<IReadOnlyCollection<BattStatus>> onChanged, CancellationToken token)
        {
            try
            {
                var lastStatuses = Array.Empty<BattStatus>() as IReadOnlyCollection<BattStatus>;
                while (!token.IsCancellationRequested)
                {
                    var count = this.DecreaseIntervalCount();
                    if (count == 0)
                    {
                        try
                        {
                            Debug.WriteLine("Scan Devices");
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
                    }
                    await Task.Delay(INTERVAL_MS, token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            Debug.WriteLine("BTConnBattMonitor.WatchAsync: canceled");
        }

        public async void StartMonitor(Action<IReadOnlyCollection<BattStatus>> onChange)
        {
            await this.StopMonitorAsync();
            _cts = new CancellationTokenSource();
            _task = Task.Run(async () =>
            {
                await this.WatchAsync(onChange, _cts.Token);
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

        public void ScanFast()
        {
            lock (this)
            {
                _intervalCount = 0;
                _fastScanCount = FAST_SCAN_COUNT;
            }
        }
    }
}
