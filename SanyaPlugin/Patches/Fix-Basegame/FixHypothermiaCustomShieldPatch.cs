using HarmonyLib;
using InventorySystem.Items.Usables.Scp244.Hypothermia;
using PlayableScps.Interfaces;
using PlayerStatsSystem;
using UnityEngine;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(HumeShieldSubEffect), nameof(HumeShieldSubEffect.UpdateHumeShield))]
	public static class FixHypothermiaCustomShieldBlock
	{
		public static bool Prefix(HumeShieldSubEffect __instance, ref bool __result, float expo)
		{
			__result = false;
			IShielded shielded = __instance.Hub.scpsController.CurrentScp as IShielded;

			AhpStat.AhpProcess Shield = null;
			if(__instance.Hub.TryGetComponent<SanyaPluginComponent>(out var sanya) && sanya.Shield != null)
				Shield = sanya.Shield;

			if(shielded != null)
				Shield = shielded.Shield;

			if(Shield == null)
				return false;

			__instance._decreaseTimer += expo * Time.deltaTime;

			if(Shield.CurrentAmount < 1f || __instance._decreaseTimer < __instance._hsDecreaseStartTime)
				return false;

			float num = (expo * __instance._hsDecreasePerExposure + __instance._hsDecreaseAbsolute) * Time.deltaTime;
			if(num < Shield.CurrentAmount)
				Shield.CurrentAmount -= num;

			if(Shield.CurrentAmount <= 0f)
				return false;

			Shield.SustainTime = Mathf.Max(Shield.SustainTime, __instance._hsSustainTime);
			__result = true;
			return false;
		}
	}
}
