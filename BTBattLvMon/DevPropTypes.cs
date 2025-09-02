using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTBattLvMon
{
    public static class DevPropTypes
    {
        public const uint DEVPROP_TYPE_NULL = 0x00000000;
        public const uint DEVPROP_TYPE_STRING = 0x00000012;
        public const uint DEVPROP_TYPE_STRING_LIST = 0x00000013;
        public const uint DEVPROP_TYPE_BOOLEAN = 0x00000011;
        public const uint DEVPROP_TYPE_GUID = 0x0000000d;
        public const uint DEVPROP_TYPE_BYTE = 0x00000003;

        public const uint DEVPROP_TYPEMOD_LIST = 0x2000;

        public static bool IsStringList(uint propertyType)
        {
            if (propertyType == DEVPROP_TYPE_STRING_LIST)
            {
                return true;
            }
            if (propertyType == (DEVPROP_TYPE_STRING | DEVPROP_TYPEMOD_LIST))
            {
                return true;
            }
            return false;
        }
    }
}
