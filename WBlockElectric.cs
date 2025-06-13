using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WBlockElectric", "wakanda | AI", "1.0.1")]
    [Description("Блокирует крафт и установку перезаряжаемых аккумуляторов")]
    public class WBlockElectric : RustPlugin
    {
        private readonly HashSet<string> _blockedItems = new HashSet<string>
        {
            "electric.battery.rechargable.small",
            "electric.battery.rechargable.large"
        };

        private Plugin GameTipHelper;

        private void Init()
        {
            GameTipHelper = plugins.Find("GameTipHelper");
        }

        private void OnItemCraft(ItemCraftTask task, BasePlayer player)
        {
            if (task.blueprint.targetItem == null) return;
            
            var shortname = task.blueprint.targetItem.shortname;
            if (_blockedItems.Contains(shortname))
            {
                task.cancelled = true;
                ShowErrorMessage(player);
            }
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            var player = deployer.ToPlayer();
            if (player == null) return;
            
            var item = entity?.GetItem();
            if (item == null) return;
            
            var shortname = item.info.shortname;
            if (_blockedItems.Contains(shortname))
            {
                // Возвращаем предмет в инвентарь игрока
                var returnedItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                if (returnedItem != null)
                {
                    player.GiveItem(returnedItem);
                }
                
                entity.Kill();
                ShowErrorMessage(player);
            }
        }

        private void ShowErrorMessage(BasePlayer player)
        {
            if (GameTipHelper != null)
            {
                GameTipHelper.Call("ShowError", player, "Не работает на девблоге, используя прямое подключение!", 4f);
            }
            else
            {
                player.ChatMessage("<color=red>Не работает на девблоге, используя прямое подключение!</color>");
            }
        }
    }
}