using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WCheckMonuments", "wakanda | AI", "1.0.0")]
    [Description("Проверяет наличие Космодрома и Аирфилда на карте")]

    class WCheckMonuments : RustPlugin
    {
		private void OnServerInitialized()
		{
			MonumentInfo[] monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();

			bool hasLaunchsite = false;
			bool hasAirfield = false;

			foreach (MonumentInfo monument in monuments)
			{
				if (monument.name.Contains("launch_site_1"))
				{
					hasLaunchsite = true;
				}

				if (monument.name.Contains("airfield_1"))
				{
					hasAirfield = true;
				}
			}

			if (hasLaunchsite)
			{
				PrintWarning("На карте присутствует Космодром");
			}
			else
			{
				PrintWarning("Космодром отсутствует на карте.");
			}

			if (hasAirfield)
			{
				PrintWarning("На карте присутствует Аирфилд");
			}
			else
			{
				PrintWarning("Аирфилд отсутствует на карте.");
			}
		}
    }
}