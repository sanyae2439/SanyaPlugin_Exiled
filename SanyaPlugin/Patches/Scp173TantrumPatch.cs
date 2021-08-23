using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.Scp173), nameof(PlayableScps.Scp173.ServerDoTantrum))]
	public static class Scp173TantrumPatch
	{
		public static void Postfix(PlayableScps.Scp173 __instance)
		{
			if(__instance._tantrumCooldownRemaining != 30f) return;
			__instance._tantrumCooldownRemaining = SanyaPlugin.Instance.Config.Scp173TantrumCooldown;
		}
	}
}
