using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.Scp173), nameof(PlayableScps.Scp173.ServerDoBreakneckSpeeds))]
	public static class Scp173BreakneckPatch
	{
		public static void Postfix(PlayableScps.Scp173 __instance) 
		{
			if(__instance._breakneckSpeedsCooldownRemaining != 40f) return;
				__instance._breakneckSpeedsCooldownRemaining = SanyaPlugin.Instance.Config.Scp173BreakneckCooldown;
		}
	}
}
