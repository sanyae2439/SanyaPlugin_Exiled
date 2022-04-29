using HarmonyLib;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.ThrowableProjectiles;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(ExplosionGrenade), nameof(ExplosionGrenade.ExplodeDoor))]
	public static class ExplodeDoorPatch
	{
		public static bool Prefix(DoorVariant dv)
		{
			if(dv is PryableDoor pd && !pd.TargetState)
			{
				pd.TryPryGate();
				return false;
			}

			if(dv is BreakableDoor breakable && !breakable._ignoredDamageSources.HasFlagFast(DoorDamageType.Grenade))
			{
				dv.NetworkTargetState = true;
				dv.ServerChangeLock(DoorLockReason.AdminCommand, true);
				dv.UnlockLater(10f, DoorLockReason.AdminCommand);
			}
			return false;
		}
	}
}
