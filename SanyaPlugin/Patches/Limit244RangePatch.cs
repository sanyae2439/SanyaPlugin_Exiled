using HarmonyLib;
using InventorySystem.Items.Usables.Scp244;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp244DeployablePickup), nameof(Scp244DeployablePickup.CurrentSizePercent), MethodType.Setter)]
	public static class Limit244RangePatch
	{
		public static void Prefix(Scp244DeployablePickup __instance, ref float value)
		{
			if(__instance.MaxDiameter != 25f)
				value = Mathf.Clamp(value, 0f, 0.25f);
		}
	}
}
