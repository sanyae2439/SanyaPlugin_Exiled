using CustomPlayerEffects;
using HarmonyLib;
using InventorySystem.Items.Usables.Scp244.Hypothermia;
using Mirror;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(DamageSubEffect), nameof(DamageSubEffect.UpdateEffect))]
	public static class Scp244AddScpsDamagePatch
	{
		public static bool Prefix(DamageSubEffect __instance)
		{
			if(!NetworkServer.active) return false;
			__instance.DealDamage(__instance._temperature.CurTemperature);
			return false;
		}
	}
}
