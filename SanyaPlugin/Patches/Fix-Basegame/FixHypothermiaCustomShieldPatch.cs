using CustomPlayerEffects;
using HarmonyLib;
using PlayableScps.Interfaces;
using PlayerStatsSystem;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(Hypothermia), nameof(Hypothermia.UpdateHume))]
	public static class FixHypothermiaCustomShieldBlock
	{
		public static bool Prefix(Hypothermia __instance)
		{
			IShielded shielded = __instance.Hub.scpsController.CurrentScp as PlayableScps.Interfaces.IShielded;
			if(!__instance.IsSCP || !__instance.IsEnabled)
				return false;

			AhpStat.AhpProcess Shield = null;
			if(__instance.Hub.TryGetComponent<SanyaPluginComponent>(out var sanya) && sanya.Shield != null)
				Shield = sanya.Shield;

			if(shielded != null)
				Shield = shielded.Shield;

			if(Shield == null)
				return false;

			if(Shield.CurrentAmount <= 0f || Shield.CurrentAmount >= Shield.Limit || Shield.SustainTime > __instance._hsSustainTime)
			{
				__instance._humeBlocked = false;
				return false;
			}

			Shield.SustainTime = __instance._hsSustainTime;

			if(!__instance._humeBlocked)
			{
				__instance._humeBlocked = true;
				__instance.Hub.networkIdentity.connectionToClient.Send(default(Hypothermia.HumeBlockMsg), 0);
			}

			return false;
		}
	}
}
