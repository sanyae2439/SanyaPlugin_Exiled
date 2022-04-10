using HarmonyLib;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.SetupShield))]
	public static class SetupShieldPatch
	{
		public static void Prefix(PlayableScps.Scp096 __instance) => __instance._curMaxShield = SanyaPlugin.Instance.Config.Scp096MaxAhp;
	}
}
