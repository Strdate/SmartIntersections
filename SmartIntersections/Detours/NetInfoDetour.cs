using Redirection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartIntersections.Detours
{
    [TargetType(typeof(NetInfo))]
    public class NetInfoDetour
    {
        public static float MinNodeDistance = 0f;

        [RedirectMethod]
        public float GetMinNodeDistance()
        {
            return MinNodeDistance;
        }
    }
}
