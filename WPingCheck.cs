using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("WPingCheck", "wakanda | AI", "1.0.0")]
    [Description("Показывает пинг игрока по команде")]
    public class WPingCheck : RustPlugin
    {
        [ChatCommand("ping")]
        private void PingCommand(BasePlayer player, string command, string[] args)
        {
            var ping = Network.Net.sv.GetAveragePing(player.Connection);

            if (plugins.Find("GameTipHelper") != null)
            {
                plugins.Find("GameTipHelper").Call("ShowError", player, $"Ваш пинг: {ping}ms", 4f);
            }
            else
            {
                SendReply(player, $"Ваш пинг: {ping}ms");
            }
        }
    }
}
