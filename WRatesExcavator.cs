using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("WRates Excavator", "wakanda | AI", "1.0.0")]
    [Description("Увеличивает рейты добычи гигантского экскаватора")]
    public class WRatesExcavator : RustPlugin
    {
        private const float ExcavatorMultiplier = 4.0f; // Во сколько раз увеличивать ресурсы

        [HookMethod("OnExcavatorGather")]
        private object OnExcavatorGather(ExcavatorArm excavator, Item item)
        {
            if (item == null || excavator == null)
                return null;

            item.amount = (int)(item.amount * ExcavatorMultiplier);
            
            return null;
        }
    }
}
