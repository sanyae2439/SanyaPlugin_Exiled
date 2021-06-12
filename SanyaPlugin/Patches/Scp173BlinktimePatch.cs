using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp173PlayerScript), nameof(Scp173PlayerScript.Start))]
	public static class Scp173BlinktimePatch
	{
		public static void Postfix(Scp173PlayerScript __instance)
		{
			if(__instance.isLocalPlayer)
			{
				__instance.minBlinkTime = SanyaPlugin.Instance.Config.Scp173MinBlinktime;
				__instance.maxBlinkTime = SanyaPlugin.Instance.Config.Scp173MaxBlinktime;
			}
		}
	}
}
