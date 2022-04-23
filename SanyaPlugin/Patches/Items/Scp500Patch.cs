using CustomPlayerEffects;
using HarmonyLib;
using InventorySystem.Items.Usables;
using PlayerStatsSystem;

namespace SanyaPlugin.Patches.Items
{
	[HarmonyPatch(typeof(Scp500), nameof(Scp500.OnEffectsActivated))]
	public static class Scp500Patch
	{
		public static void Postfix(Scp500 __instance)
		{
			__instance.Owner.playerStats.GetModule<AhpStat>().ServerAddProcess(75f);
			__instance.Owner.fpc.ResetStamina();
			__instance.Owner.playerEffectsController.EnableEffect<Invigorated>(30f);
		}
	}
}
