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
			foreach(var i in FlickerableLightController.Instances.Where(x => (x.transform.root?.name != "Outside") && x.transform.parent?.name != "PocketWorld"))
			{
				i.Network_warheadLightOverride = true;
				i.Network_warheadLightColor = new Color(0f, 0f, 0f);
			}
			yield break;
		}

		public static IEnumerator<float> InitClassDPrison()
		{
			var room = Room.Get(Exiled.API.Enums.RoomType.LczClassDSpawn);
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

		public static IEnumerator<float> CheckScp106Chamber()
		{
			yield return Timing.WaitForSeconds(5f);

			if(!Player.Get(RoleType.Scp106).Any())
			{
				ReferenceHub.HostHub.characterClassManager._lureSpj.NetworkallowContain = true;
				OneOhSixContainer.used = true;
			}
		}
	}
}
