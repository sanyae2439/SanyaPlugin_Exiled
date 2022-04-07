using HarmonyLib;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.OnDamage))]
	public static class StopEnrageOnDamagePatch
	{
		public static bool Prefix(PlayableScps.Scp096 __instance)
		{
			if(!SanyaPlugin.Instance.Config.Scp096Rework) return true;
			__instance.Shield.SustainTime = 25f;
			return false;
		}
	}
}
