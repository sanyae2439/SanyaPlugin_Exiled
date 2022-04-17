using HarmonyLib;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.ThrowableProjectiles;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(ExplosionGrenade), nameof(ExplosionGrenade.ExplodeDoor))]
	public static class ExplodeDoorPatch
	{
		public static void Prefix(DoorVariant dv)
		{
			if(dv is PryableDoor pd) 
				pd.TryPryGate();
		}
	}
}
