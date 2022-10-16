using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerStatsSystem;
using Respawning;
using SanyaPlugin.Commands.Utils;
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

			string level = data.level.ToString();
			string rolestr = player.ReferenceHub.serverRoles.GetUncoloredRoleString();
			string rolecolor = player.RankColor;
			string badge = string.Empty;

			rolestr = rolestr.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

			if(rolecolor == "light_red")
				rolecolor = "pink";

			if(data.level == -1)
				level = "???";

			if(data.level >= 50 && data.level < 100)
				rolecolor = "lime";
			else if(data.level >= 100 && data.level < 150)
				rolecolor = "aqua";
			else if(data.level >= 150 && data.level < 200)
				rolecolor = "deep_pink";
			else if(data.level >= 200 && data.level < 300)
				rolecolor = "emerald";
			else if(data.level >= 300)
				rolecolor = "magenta";

			if(string.IsNullOrEmpty(rolestr))
				badge = $"Level{level}";
			else
				badge = $"Level{level} : {rolestr}";

			if(player.Group == null)
			{
				UserGroup group = new UserGroup()
				{
					BadgeText = badge,
					BadgeColor = rolecolor,
					HiddenByDefault = false,
					Cover = true,
					KickPower = 0,
					Permissions = 0,
					RequiredKickPower = 0,
					Shared = false
				};
				player.ReferenceHub.serverRoles.SetGroup(group, false, false, true);
			}
			else
			{
				player.ReferenceHub.serverRoles.Network_myText = badge;
				player.ReferenceHub.serverRoles.Network_myColor = rolecolor;
			}

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
			while(Generator.Get(Exiled.API.Enums.GeneratorState.Engaged).Count() < 2)
			{
				FlickerableLightController.Instances.Where(x => x.LightsEnabled).Random()?.ServerFlickerLights(30f);
				yield return Timing.WaitForSeconds(1f);
			}
			yield break;
		}

		public static IEnumerator<float> InitClassDPrison()
		{
			var room = Room.Get(Exiled.API.Enums.RoomType.LczClassDSpawn);
			var doors = room.Doors.Where(x => x.Type == Exiled.API.Enums.DoorType.PrisonDoor);

			room.TurnOffLights(10f);

			foreach(var i in doors)
			{
				i.Base.ServerChangeLock(Interactables.Interobjects.DoorUtils.DoorLockReason.AdminCommand, true);
				i.Base.NetworkTargetState = false;
			}

			yield return Timing.WaitForSeconds(10f);

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

		public static IEnumerator<float> Scp106CustomTeleport(Player player, Vector3 position)
		{
			if(!player.ReferenceHub.scp106PlayerScript.goingViaThePortal)
			{
				player.ReferenceHub.scp106PlayerScript.RpcTeleportAnimation();
				player.ReferenceHub.scp106PlayerScript.goingViaThePortal = true;
				yield return Timing.WaitForSeconds(2.5f);
				player.Position = position;
				yield return Timing.WaitForSeconds(2.5f);
				if(AlphaWarheadController.Host.detonated && player.Position.y < 800f)
					player.ReferenceHub.scp106PlayerScript._hub.playerStats.DealDamage(new WarheadDamageHandler());
				player.ReferenceHub.scp106PlayerScript.goingViaThePortal = false;
			}
		}

		public static IEnumerator<float> TryRespawnDisconnectedScp(RoleType role, float health)
		{
			Log.Info("[TryRespawnDisconnectedScp] Start");
			while(true)
			{
				yield return Timing.WaitForSeconds(10f);
				if(role == RoleType.Scp173 && Map.IsLczDecontaminated) yield break;
				if(role == RoleType.Scp106 && OneOhSixContainer.used) yield break;
				if(!RoundSummary.RoundInProgress()) yield break;

				var spectators = Player.Get(RoleType.Spectator).Where(x => !x.IsOverwatchEnabled);
				if(!spectators.Any()) continue;

				var target = spectators.Random();
				target.SetRole(role);
				if(target.GameObject.TryGetComponent<SanyaPluginComponent>(out var sanya))
					sanya.AddHudBottomText("<color=#ff0000><size=25>SCPのプレイヤーが切断したため、代わりとして選ばれました。</size></color>", 5);
				yield return Timing.WaitForSeconds(1f);

				target.Health = health;
				if(target.MaxArtificialHealth > 0) target.ArtificialHealth = 0f;

				Log.Info("[TryRespawnDisconnectedScp] Done");
				yield break;
			}
		}

		public static IEnumerator<float> RainbowFacility()
		{
			var rooms = Room.List.Where(x => x.Zone == ZoneType.LightContainment || x.Zone == ZoneType.HeavyContainment || x.Zone == ZoneType.Entrance);
			foreach(var i in rooms)
				i.FlickerableLightController.Network_warheadLightOverride = true;

			Log.Info($"[RainbowFacility] Start. Targets:{rooms.Count()}");
			while(RainbowFacilityCommand.isActive)
			{
				var currentColor = Color.HSVToRGB(Time.time % 1, 1, 1);
				foreach(var i in rooms)
					i.FlickerableLightController.Network_warheadLightColor = currentColor;
				yield return Timing.WaitForOneFrame;
			}
			Log.Info("[RainbowFacility] End");

			foreach(var i in rooms)
			{
				i.FlickerableLightController.Network_warheadLightOverride = false;
				i.FlickerableLightController.Network_warheadLightColor = FlickerableLightController.DefaultWarheadColor;
			}
		}

		public static IEnumerator<float> Scp079ScanningHumans(Player player)
		{
			player.ReferenceHub.scp079PlayerScript._serverIndicatorUpdateTimer = -12f;
			player.ReferenceHub.scp079PlayerScript.TargetSetupIndicators(player.Connection, new List<Vector3>());

			int counter = 0;
			while(counter++ < 10)
			{
				var list = new List<Vector3>();
				if(player.Role != RoleType.Scp079) break;
				if(player.CurrentRoom.Zone == ZoneType.Surface) continue;

				foreach(var target in Player.List.Where(x => x.IsHuman && x.Zone == player.Zone))
					list.Add(target.CameraTransform.position);

				player.ReferenceHub.scp079PlayerScript.TargetSetupIndicators(player.Connection, list);
				yield return Timing.WaitForSeconds(1.3f);
			}
			player.ReferenceHub.scp079PlayerScript.TargetSetupIndicators(player.Connection, new List<Vector3>());
		}

		public static IEnumerator<float> Scp079RoomFlashing(Player player)
		{
			Methods.SpawnGrenade(player.CurrentRoom.Position + Vector3.up, ItemType.GrenadeFlash, 0.35f, player.ReferenceHub);
			yield return Timing.WaitForSeconds(0.35f);
			foreach(var target in player.CurrentRoom.Players.Where(x => x.IsHuman))
			{
				target.EnableEffect<Flashed>(2f, true);
				player.SendHitmarker();
			}
			yield return Timing.WaitForSeconds(0.25f);
			player.CurrentRoom.TurnOffLights(8f);
		}

		public static IEnumerator<float> Scp079PlayDummySound(Player player)
		{
			var speaker = Methods.GetCurrentRoomsSpeaker(player.CurrentRoom);
			var itemtype = new List<ItemType>() 
			{
				ItemType.GunAK, 
				ItemType.GunE11SR, 
				ItemType.GunLogicer,
				ItemType.GunCrossvec, 
				ItemType.GunFSP9 
			}.GetRandomOne();
			if(speaker == null) yield break;

			int counter = 0;
			while(counter++ < 15)
			{
				foreach(var i in Player.List)
					Methods.PlayGunSoundFixed(i, speaker.transform.position, itemtype, 120);
				yield return Timing.WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.2f));
			}
		}
	}
}
 