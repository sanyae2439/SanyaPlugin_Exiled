using HarmonyLib;
using InventorySystem.Items.ThrowableProjectiles;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(TimeGrenade), nameof(TimeGrenade.UserCode_RpcSetTime))]
	public static class FixGrenadeDestroyPatch
	{
		public static void Prefix(ref float time) => time += 0.1f;
	}
}
