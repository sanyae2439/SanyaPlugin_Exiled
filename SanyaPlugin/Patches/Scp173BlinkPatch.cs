using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.Scp173), nameof(PlayableScps.Scp173.ServerHandleBlinkMessage))]
	public static class Scp173BlinkPatch
	{
		public static void Postfix(PlayableScps.Scp173 __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp173BlinkCooldown != -1f)
				__instance._blinkCooldownRemaining = SanyaPlugin.Instance.Config.Scp173BlinkCooldown;
		}
	}
}