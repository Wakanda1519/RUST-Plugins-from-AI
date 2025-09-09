using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WChainsawHit", "wakanda | AI", "1.0.0")]
    [Description("Бензопила всегда попадает по крестику при добыче дерева.")]
    public class WChainsawHit : RustPlugin
    {
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            // Проверяем, что это игрок с бензопилой добывает дерево
            if (entity == null || !(entity is BasePlayer)) return;
            
            BasePlayer player = entity as BasePlayer;
            Item activeItem = player.GetActiveItem();
            
            // Проверяем, что активный предмет - бензопила
            if (activeItem?.info?.shortname != "chainsaw") return;
            
            // Проверяем, что добывается дерево
            if (item.info.shortname != "wood") return;
            
            // Проверяем, что диспенсер - это дерево
            if (dispenser.gatherType != ResourceDispenser.GatherType.Tree) return;
            
            // Добавляем бонус 50% дерева (как будто попал по крестику)
            int bonusAmount = Mathf.RoundToInt(item.amount * 0.5f);
            item.amount += bonusAmount;
        }
    }
}
