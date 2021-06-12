using System;
using Grenades;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Grenade), nameof(Grenade.ServersideExplosion))]
	public static class PreventGrenadeLogPatch
	{
		public static bool Prefix(Grenade __instance, ref bool __result)
		{
			try
			{
				if(__instance.thrower?.name != "Host")
				{
					string text = (__instance.thrower != null) ? (__instance.thrower.hub.characterClassManager.UserId + " (" + __instance.thrower.hub.nicknameSync.MyNick + ")") : "(UNKNOWN)";
					ServerLogs.AddLog(ServerLogs.Modules.Logger, "Player " + text + "'s " + __instance.logName + " grenade exploded.", ServerLogs.ServerLogType.GameEvent);
				}
				__result = true;
				return false;
			}
			catch(Exception)
			{
				return true;
			}
		}
	}
}
