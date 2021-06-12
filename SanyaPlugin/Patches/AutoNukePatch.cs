using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.StartDetonation))]
	public static class AutoNukePatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static void Prefix(AlphaWarheadController __instance)
		{
			if(__instance._autoDetonate && __instance._autoDetonateTimer <= 0f)
			{
				__instance.InstantPrepare();
			}
		}
	}
}
