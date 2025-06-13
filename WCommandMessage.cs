using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WCommandMessage", "wakanda | AI", "1.0.0")]
    [Description("Отправляет сообщение в чат при вводе определенных команд")]
    public class WCommandMessage : RustPlugin
    {
        [PluginReference]
        private Plugin GameTipHelper;
        
        private PluginSettings _settings = new PluginSettings
        {
			// Настройка плагина
            Commands = new Dictionary<string, string>
            {
                {"event", "Система сейчас не доступна!"}
            },
            Prefix = ""
        };

        private void Init()
        {
            foreach (var command in _settings.Commands.Keys)
            {
                cmd.AddChatCommand(command, this, "HandleCommand");
            }
        }

        private void HandleCommand(BasePlayer player, string command, string[] args)
        {
            string message;
            if (_settings.Commands.TryGetValue(command, out message))
            {
                string fullMessage = string.Format("{0} {1}", _settings.Prefix, message);
                
                if (GameTipHelper != null)
                    GameTipHelper.Call("ShowError", player, fullMessage, 4f);
                else
                    SendReply(player, fullMessage);
            }
        }

        private class PluginSettings
        {
            public Dictionary<string, string> Commands { get; set; }
            public string Prefix { get; set; }
        }
    }
}