using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.Scp173), nameof(PlayableScps.Scp173.ServerDoBreakneckSpeeds))]
	public static class Scp173BreakneckPatch
	{
		public static bool Prefix(PlayableScps.Scp173 __instance)
		{
			if(__instance._breakneckSpeedsCooldownRemaining > 0f)
				return false;
			__instance.BreakneckSpeedsActive = !__instance.BreakneckSpeedsActive;
			if(!__instance.BreakneckSpeedsActive)
				__instance._breakneckSpeedsCooldownRemaining = SanyaPlugin.Instance.Config.Scp173RemoveBreakneckCooldown ? 0f : 40f;
			return false;
		}
	}
}
