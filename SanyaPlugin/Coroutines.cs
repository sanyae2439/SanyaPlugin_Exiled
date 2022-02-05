using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using Respawning;
using UnityEngine;

namespace SanyaPlugin
{
	public static class Coroutines
	{
		public static IEnumerator<float> GrantedLevel(Player player, PlayerData data)
		{
			yield return Timing.WaitForSeconds(1f);

			if(player.DoNotTrack)
			{
				Log.Info($"[GrantedLevel] User has DNT : {player.DoNotTrack}");
				SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Remove(player.UserId);
				yield break;
			}

			if(player.GlobalBadge != null)
			{
				Log.Info($"[GrantedLevel] User has GlobalBadge {player.UserId}:{player.GlobalBadge?.Text}");
				yield break;
			}

			var group = player.Group?.Clone();
			string level = data.level.ToString();
			string rolestr = player.ReferenceHub.serverRoles.GetUncoloredRoleString();
			string rolecolor = player.RankColor;
			string badge = string.Empty;

			rolestr = rolestr.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

			if(rolecolor == "light_red")
				rolecolor = "pink";

			if(data.level == -1)
				level = "???";

			if(!player.DoNotTrack)
			{
				if(string.IsNullOrEmpty(rolestr))
					badge = $"Level{level}";
				else
					badge = $"Level{level} : {rolestr}";
			}

			if(group == null)
				group = new UserGroup()
				{
					BadgeText = badge,
					BadgeColor = "default",
					HiddenByDefault = false,
					Cover = true,
					KickPower = 0,
					Permissions = 0,
					RequiredKickPower = 0,
					Shared = false
				};
			else
			{
				group.BadgeText = badge;
				group.BadgeColor = rolecolor;
				group.HiddenByDefault = false;
				group.Cover = true;
			}

			player.ReferenceHub.serverRoles.SetGroup(group, false, false, true);

			Log.Info($"[GrantedLevel] {player.UserId} : Level{level}");

			yield break;
		}

		public static IEnumerator<float> BigHitmarker(Player player, float size = 1f)
		{
			yield return Timing.WaitForSeconds(0.1f);
			player.SendHitmarker(size);
			yield break;
		}

		public static IEnumerator<float> InitBlackout()
		{
			yield return Timing.WaitForSeconds(10f);
			Methods.SendSubtitle(SanyaPlugin.Instance.Translation.BlackoutInit, 20);
			RespawnEffectsController.PlayCassieAnnouncement("warning . facility power system has been attacked . all most containment zones light does not available until generator activated .", false, true);
			foreach(var x in FlickerableLightController.Instances)
				x.ServerFlickerLights(5f);
			yield return Timing.WaitForSeconds(3f);
			foreach(var i in FlickerableLightController.Instances.Where(x => x.transform.root?.name != "Outside"))
			{
				i.Network_warheadLightOverride = true;
				i.Network_warheadLightColor = new Color(0f, 0f, 0f);
			}
			yield break;
		}

		public static IEnumerator<float> InitClassDPrison()
		{
			var room = Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.LczClassDSpawn);
			var doors = room.Doors.Where(x => x.Type == Exiled.API.Enums.DoorType.PrisonDoor);

			room.TurnOffLights(5f);

			foreach(var i in doors)
			{
				i.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
				i.Base.NetworkTargetState = false;
			}

			yield return Timing.WaitForSeconds(5f);

			foreach(var j in doors)
				j.Base.NetworkTargetState = true;
			yield break;
		}

		public static IEnumerator<float> CheckScpsRoom()
		{
			yield return Timing.WaitForSeconds(3f);

			string text = string.Empty;
			bool detect939 = false;
			foreach(var i in ReferenceHub.HostHub.characterClassManager.Classes.Where(x => x.team == Team.SCP && x.roleId != RoleType.Scp0492 && x.roleId != RoleType.Scp079 && !x.banClass))
			{
				text += $"{i.roleId}:{i.banClass}\n";

				switch(i.roleId)
				{
					case RoleType.Scp173:
						var gate173 = Map.GetDoorByName("173_GATE");
						gate173?.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
						break;
					case RoleType.Scp096:
						var door096 = Map.GetDoorByName("096");
						door096?.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);

						var itemcard = Map.Pickups.FirstOrDefault(x => x.Type == ItemType.KeycardNTFLieutenant);
						itemcard?.Destroy();
						Methods.SpawnItem(ItemType.KeycardNTFLieutenant, RoleType.Scp096.GetRandomSpawnProperties().Item1);
						break;
					case RoleType.Scp049:
						var lift = Map.Lifts.First(x => x.elevatorName == "SCP-049");
						lift.NetworkstatusID = (byte)Lift.Status.Down;
						lift.Network_locked = true;
						break;
					case RoleType.Scp106:
						var gate106p = Map.GetDoorByName("106_PRIMARY");
						var gate106s = Map.GetDoorByName("106_SECONDARY");
						gate106p?.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
						gate106s?.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
						break;
					case RoleType.Scp93953:
					case RoleType.Scp93989:
						if(!detect939)
						{
							detect939 = true;
							break;
						}
						else
						{
							foreach(var door in Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.Hcz939).Doors)
								door.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
							break;
						}
				}
			}
		}
	}
}
