using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Grenades;
using HarmonyLib;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(FragGrenade), nameof(FragGrenade.ServersideExplosion))]
	public static class FragGrenadePatch
	{
		public static bool Prefix(FragGrenade __instance, ref bool __result)
		{
			Vector3 position = __instance.transform.position;
			HashSet<DoorVariant> lockDoors = new HashSet<DoorVariant>();
			int num = 0;
			foreach(Collider collider in Physics.OverlapSphere(position, __instance.chainTriggerRadius, __instance.damageLayerMask))
			{
				if(collider.TryGetComponent<BreakableWindow>(out BreakableWindow window) && (window.transform.position - position).sqrMagnitude <= __instance.sqrChainTriggerRadius)
					window.ServerDamageWindow(500f);

				PryableDoor pryableDoor = collider.GetComponentInParent<PryableDoor>();
				if(pryableDoor != null && !pryableDoor.NetworkTargetState)
					pryableDoor.TryPryGate();

				DoorVariant door = collider.GetComponentInParent<DoorVariant>();
				if(door != null && !door.NetworkTargetState && door is IDamageableDoor) 
				{
					door.NetworkTargetState = true;
					door.ServerChangeLock(DoorLockReason.Lockdown079, true);
					lockDoors.Add(door);
					//damageableDoor.ServerDamage(__instance.damageOverDistance.Evaluate(Vector3.Distance(position, door.transform.position)), DoorDamageType.Grenade);
				}

				if((__instance.chainLengthLimit == -1 || __instance.chainLengthLimit > __instance.currentChainLength) && (__instance.chainConcurrencyLimit == -1 || __instance.chainConcurrencyLimit > num))
				{
					Pickup componentInChildren = collider.GetComponentInChildren<Pickup>();
					if(componentInChildren != null && __instance.ChangeIntoGrenade(componentInChildren))
						num++;
				}
			}

			if(lockDoors.Count > 0)
				ReferenceHub.HostHub.scp079PlayerScript._scheduledUnlocks.Add(Time.realtimeSinceStartup + 5f, lockDoors);

			foreach(KeyValuePair<GameObject, ReferenceHub> keyValuePair in ReferenceHub.GetAllHubs())
			{
				if(ServerConsole.FriendlyFire || !(keyValuePair.Key != __instance.thrower.gameObject) || (keyValuePair.Value.weaponManager.GetShootPermission(__instance.throwerTeam, false) && keyValuePair.Value.weaponManager.GetShootPermission(__instance.TeamWhenThrown, false)))
				{
					PlayerStats playerStats = keyValuePair.Value.playerStats;
					if(!(playerStats == null) && playerStats.ccm.InWorld)
					{
						float num2 = __instance.damageOverDistance.Evaluate(Vector3.Distance(position, playerStats.transform.position)) * (playerStats.ccm.IsHuman() ? GameCore.ConfigFile.ServerConfig.GetFloat("human_grenade_multiplier", 0.7f) : GameCore.ConfigFile.ServerConfig.GetFloat("scp_grenade_multiplier", 1f));
						if(num2 > __instance.absoluteDamageFalloff)
						{
							foreach(Transform transform in playerStats.grenadePoints)
							{
								if(!Physics.Linecast(position, transform.position, __instance.hurtLayerMask))
								{
									playerStats.HurtPlayer(new PlayerStats.HitInfo(num2, (__instance.thrower != null) ? __instance.thrower.hub.LoggedNameFromRefHub() : "(UNKNOWN)", DamageTypes.Grenade, __instance.thrower.hub.queryProcessor.PlayerId), keyValuePair.Key, false, true);
									break;
								}
							}
							if(!playerStats.ccm.IsAnyScp())
							{
								float duration = __instance.statusDurationOverDistance.Evaluate(Vector3.Distance(position, playerStats.transform.position));
								keyValuePair.Value.playerEffectsController.EnableEffect(keyValuePair.Value.playerEffectsController.GetEffect<Burned>(), duration, false);
								keyValuePair.Value.playerEffectsController.EnableEffect(keyValuePair.Value.playerEffectsController.GetEffect<Concussed>(), duration, false);
							}
						}
					}
				}
			}
			__result = ((Func<bool>)Activator.CreateInstance(typeof(Func<bool>), __instance, typeof(EffectGrenade).GetMethod(nameof(EffectGrenade.ServersideExplosion)).MethodHandle.GetFunctionPointer()))();
			return false;
		}
	}
}
