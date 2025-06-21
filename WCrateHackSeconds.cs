using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("WCrateHackSeconds", "wakanda | AI", "1.0.0")]
    [Description("Устанавливает время взлома для взламываемых ящиков")]
    public class WCrateHackSeconds : RustPlugin
    {
        private const float HackSeconds = 420f; // 7 minutes
        
        void Init()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "hackablelockedcrate.requiredhackseconds", HackSeconds);
        }
    }
}
