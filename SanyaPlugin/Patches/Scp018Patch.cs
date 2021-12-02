using System;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp018Projectile), nameof(Scp018Projectile.ProcessCollision))]
	public static class Scp018Patch
	{
		public static bool Prefix(Scp018Projectile __instance, Collision collision)
		{
			__instance.CallBaseMethod<Action<Collision>>(typeof(CollisionDetectionPickup), nameof(CollisionDetectionPickup.ProcessCollision))(collision);
			__instance.RpcMakeSound(collision.relativeVelocity.sqrMagnitude);

			if(__instance._activatedTime == 0f)
			{
				if(collision.relativeVelocity.sqrMagnitude < __instance._activationSqrt)
					return false;
				__instance.ServerActivate();
				__instance._activatedTime = Time.timeSinceLevelLoad;
			}

			if(!SanyaPlugin.Instance.Config.Scp018DisableDestroyingDoor && __instance.TryGetDoor(collision, out var damageableDoor))
			{
				float num = __instance.CurrentDamage * 300f;
				if(num >= 10f)
					damageableDoor.ServerDamage(num, DoorDamageType.Grenade);
			}

			if(__instance.Rb.velocity.sqrMagnitude < __instance._cutoffSqrt)
				__instance.Rb.velocity *= __instance._velocityMultiplier;

			return false;
		}
	}
}
