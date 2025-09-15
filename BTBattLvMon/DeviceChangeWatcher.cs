using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTBattLvMon
{
    internal class DeviceChangeWatcher: IDisposable
    {
        // 定数定義
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVNODES_CHANGED = 0x0007;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        private Action _eventHandler;
        private IntPtr _notificationHandle;
        private bool disposedValue;

        // 複数クラスに対応するときは devClassGuid を配列にしたコンストラクタを追加する
        // _notificationHandle も配列にする

        public DeviceChangeWatcher(IntPtr hWhd, Guid devClassGuid, Action eventHander)
        {
            _eventHandler = eventHander;

            DEV_BROADCAST_DEVICEINTERFACE dbdi = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_classguid = devClassGuid
            };

            IntPtr buffer = Marshal.AllocHGlobal(dbdi.dbcc_size);
            Marshal.StructureToPtr(dbdi, buffer, false);

//            _notificationHandle = RegisterDeviceNotification(hWhd, buffer, DEVICE_NOTIFY_WINDOW_HANDLE);
        }

        public bool HandleDeviceChangeMsg(ref Message m)
        {
            if (m.Msg == WM_DEVICECHANGE)
            {
                Debug.WriteLine($"Device change message received: WParam=0x{m.WParam:X}, LParam=0x{m.LParam:X}");
                switch ((int)m.WParam)
                {
                    /*
                    case DBT_DEVICEARRIVAL:
                        // no break. fall through
                    case DBT_DEVICEREMOVECOMPLETE:
                        _eventHandler();
                        return true;
                    */
                    case DBT_DEVNODES_CHANGED:
                        _eventHandler();
                        return true;
                }
            }
            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }
                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します

                if (_notificationHandle != IntPtr.Zero)
                {
                    UnregisterDeviceNotification(_notificationHandle);
                    _notificationHandle = IntPtr.Zero;
                }
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~DeviceChangeWatcher()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterDeviceNotification(IntPtr handle);

        // 構造体定義
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
        }
    }
}
