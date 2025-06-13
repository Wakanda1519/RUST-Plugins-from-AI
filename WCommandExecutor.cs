using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WCommandExecutor", "wakanda | AI", "1.0.0")]
    [Description("Выполняет консольные команды с интервалом")]
    public class WCommandExecutor : RustPlugin
    {
        private readonly List<string> commands = new List<string>
        {
            "removegroup 76561199048792741 vip",
            "removegroup 76561199113309538 vip",
            "removegroup 76561199504012301 vip"
        };

        private int currentCommandIndex = 0;
        private Timer commandTimer;

        private void Init()
        {
            commandTimer = timer.Every(0.5f, ExecuteNextCommand);
        }

        private void ExecuteNextCommand()
        {
            if (currentCommandIndex >= commands.Count)
            {
                commandTimer.Destroy();
                return;
            }

            ConsoleSystem.Run(ConsoleSystem.Option.Server, commands[currentCommandIndex]);
            currentCommandIndex++;
        }
    }
}