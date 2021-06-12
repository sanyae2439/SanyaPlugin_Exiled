using System;
using CustomPlayerEffects;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp106PlayerScript), nameof(Scp106PlayerScript.CallCmdMovePlayer))]
	public static class Scp106RemoveRangeCheckPatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp106PlayerScript __instance, GameObject ply, int t)
		{
			try
			{
				if(!__instance._iawRateLimit.CanExecute(true) || !__instance.iAm106 || !ServerTime.CheckSynchronization(t) || ply == null)
					return false;

				ReferenceHub hub = ReferenceHub.GetHub(ply);
				if(!hub.characterClassManager.IsHuman() || hub.characterClassManager.GodMode || hub.characterClassManager.CurRole.team == Team.SCP)
					return false;

				var instanceHub = ReferenceHub.GetHub(__instance.gameObject);
				instanceHub.characterClassManager.RpcPlaceBlood(ply.transform.position, 1, 2f);
				__instance.TargetHitMarker(__instance.connectionToClient, __instance.captureCooldown);
				__instance._currentServerCooldown = __instance.captureCooldown;

				if(Scp106PlayerScript._blastDoor.isClosed)
				{
					instanceHub.characterClassManager.RpcPlaceBlood(ply.transform.position, 1, 2f);
					instanceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(500f, instanceHub.LoggedNameFromRefHub(), DamageTypes.Scp106, instanceHub.playerId), ply);
				}
				else
				{
					Scp079Interactable.ZoneAndRoom otherRoom = hub.scp079PlayerScript.GetOtherRoom();
					Scp079Interactable.InteractableType[] filter = new Scp079Interactable.InteractableType[]
					{
							Scp079Interactable.InteractableType.Door, Scp079Interactable.InteractableType.Light,
							Scp079Interactable.InteractableType.Lockdown, Scp079Interactable.InteractableType.Tesla,
							Scp079Interactable.InteractableType.ElevatorUse,
					};

					foreach(Scp079PlayerScript scp079PlayerScript in Scp079PlayerScript.instances)
					{
						bool flag = false;

						foreach(Scp079Interaction scp079Interaction in scp079PlayerScript.ReturnRecentHistory(12f, filter))
							foreach(Scp079Interactable.ZoneAndRoom zoneAndRoom in scp079Interaction.interactable.currentZonesAndRooms)
								if(zoneAndRoom.currentZone == otherRoom.currentZone && zoneAndRoom.currentRoom == otherRoom.currentRoom)
									flag = true;

						if(flag)
							scp079PlayerScript.RpcGainExp(ExpGainType.PocketAssist, hub.characterClassManager.CurClass);
					}

					var ev = new Exiled.Events.EventArgs.EnteringPocketDimensionEventArgs(Exiled.API.Features.Player.Get(ply), Vector3.down * 1998.5f, Exiled.API.Features.Player.Get(instanceHub));

					Exiled.Events.Handlers.Player.OnEnteringPocketDimension(ev);

					if(!ev.IsAllowed)
						return false;

					hub.playerMovementSync.OverridePosition(ev.Position, 0f, true);

					instanceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(40f, instanceHub.LoggedNameFromRefHub(), DamageTypes.Scp106, instanceHub.playerId), ply);
				}

				PlayerEffectsController effectsController = hub.playerEffectsController;
				effectsController.GetEffect<Corroding>().IsInPd = true;
				effectsController.EnableEffect<Corroding>(0f, false);

				return false;
			}
			catch(Exception e)
			{
				Exiled.API.Features.Log.Error($"{typeof(Scp106RemoveRangeCheckPatch).FullName}:\n{e}");

				return true;
			}
		}
	}
}
