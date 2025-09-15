using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTBattLvMon
{
    public readonly record struct BattStatus(string InstanceId, string FriendlyName, int BatteryLevel);

}
