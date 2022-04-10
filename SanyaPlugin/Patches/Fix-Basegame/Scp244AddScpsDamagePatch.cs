using CustomPlayerEffects;
using HarmonyLib;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(Hypothermia), nameof(Hypothermia.Enabled))]
	public static class Scp244AddScpsDamagePatch
	{
		public static void Postfix(Hypothermia __instance) => __instance._dealScpDamage = true;
	}
}
