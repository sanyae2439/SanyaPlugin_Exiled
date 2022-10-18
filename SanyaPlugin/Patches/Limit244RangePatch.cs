using HarmonyLib;
using InventorySystem.Items.Usables.Scp244;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp244DeployablePickup), nameof(Scp244DeployablePickup.Network_syncSizePercent), MethodType.Setter)]
	public static class Limit244RangePatch
	{
		public static void Prefix(Scp244DeployablePickup __instance, ref byte value)
		{
			if(__instance.MaxDiameter != 25f)
				value = (byte)Mathf.Clamp(value, 0, 51);
		}
	}
}
