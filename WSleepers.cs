using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WSleepers", "wakanda | AI", "1.0.0")]
    [Description("Удаляет и убивает всех спящих игроков при загрузке сервера и при отключении игрока")]
    public class WSleepers : RustPlugin
    {
        private void OnServerInitialized()
        {
            RemoveAllSleepers();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveAllSleepers();
        }

        private void RemoveAllSleepers()
        {
            int count = 0;
            foreach (var sleeper in BasePlayer.sleepingPlayerList.ToList())
            {
                if (sleeper != null)
                {
                    sleeper.Kill();
                    count++;
                }
            }
        }
    }
}
