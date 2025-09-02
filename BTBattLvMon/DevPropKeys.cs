using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BTBattLvMon
{
    public static class DevPropKeys
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            public override string ToString()
            {
                return $"{fmtid:B} {pid}";
            }
        }

        public static readonly DEVPROPKEY DEVPKEY_Device_ClassGuid = new()
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 10
        };

        public static readonly DEVPROPKEY DEVPKEY_Device_CompatibleIds = new()
        {
            fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
            pid = 4
        };

        public static readonly DEVPROPKEY DEVPKEY_Device_ContainerId = new()
        {
            fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
            pid = 2
        };

        public static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName = new()
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 14
        };
    }
}
