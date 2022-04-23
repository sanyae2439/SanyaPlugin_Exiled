using HarmonyLib;
using InventorySystem.Items.Usables;

namespace SanyaPlugin.Patches.Items
{
	[HarmonyPatch(typeof(Painkillers), nameof(Painkillers.OnEffectsActivated))]
	public static class PainKillersPatch
	{
		public static void Postfix(Painkillers __instance) => __instance.Owner.fpc.ResetStamina();
	}
}
