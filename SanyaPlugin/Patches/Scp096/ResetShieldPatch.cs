using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.ResetShield))]
	public static class ResetShieldPatch
	{
		public static bool Prefix(PlayableScps.Scp096 __instance)
		{
			__instance.CurMaxShield = SanyaPlugin.Instance.Config.Scp096MaxAhp;
			__instance.ShieldAmount = Mathf.Min(__instance.ShieldAmount, __instance.CurMaxShield);
			return false;
		}
	}
}
