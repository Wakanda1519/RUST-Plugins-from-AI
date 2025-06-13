using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WHeightCheck", "wakanda | AI", "1.0.0")]
    [Description("Показывает расстояние до земли под игроком")]
    public class WHeightCheck : RustPlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Syntax"] = "Используйте: /hc"
            }, this);
        }

        [ChatCommand("hc")]
        private void HeightCheckCommand(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            var startPos = player.eyes.position;
            int groundLayer = LayerMask.GetMask("Terrain", "World");
            
            if (Physics.Raycast(startPos, Vector3.down, out hit, 1000f, groundLayer))
            {
                // Проверяем, не попали ли мы в entity
                BaseEntity hitEntity = hit.GetEntity();
                if (hitEntity != null)
                {
                    // Если попали в entity, пытаемся найти землю ниже
                    RaycastHit groundHit;
                    if (Physics.Raycast(hit.point + Vector3.down * 0.1f, Vector3.down, out groundHit, 1000f, groundLayer))
                    {
                        float height = hit.distance + groundHit.distance;
                        if (plugins.Find("GameTipHelper"))
                            plugins.Find("GameTipHelper").Call("ShowError", player, $"Расстояние до земли {height:F2}м", 4f);
                        else
                            SendReply(player, $"Расстояние до земли: {height:F2}м");
                        return;
                    }
                }

                float directHeight = hit.distance;
                if (plugins.Find("GameTipHelper"))
                    plugins.Find("GameTipHelper").Call("ShowError", player, $"Расстояние до земли {directHeight:F2}м", 4f);
                else
                    SendReply(player, $"Расстояние до земли: {directHeight:F2}м");
            }
            else
            {
                if (plugins.Find("GameTipHelper"))
                    plugins.Find("GameTipHelper").Call("ShowError", player, "Не удалось определить расстояние до земли", 4f);
                else
                    SendReply(player, "Не удалось определить расстояние до земли");
            }
        }
    }
}
