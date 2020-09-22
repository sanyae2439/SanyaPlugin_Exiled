﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
using UnityEngine;
using Mirror;
using MEC;
using Exiled.API.Features;
using HarmonyLib;
using Grenades;
using Respawning;
using Respawning.NamingRules;
using LightContainmentZoneDecontamination;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using Assets._Scripts.Dissonance;

namespace SanyaPlugin.Patches
{
	//override - Exiled 2.0
	[HarmonyPatch(typeof(Exiled.Permissions.Extensions.Permissions), nameof(Exiled.Permissions.Extensions.Permissions.CheckPermission), new Type[] { typeof(Player), typeof(string) })]
	public static class ExiledPermissionPatcher
	{
		public static bool Prefix(Player player, string permission, ref bool __result)
		{
			if(player.GameObject == PlayerManager.localPlayer)
			{
				__result = true;
				return false;
			}

			Log.Debug($"Player: {player.Nickname} UserID: {player.UserId}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
			if(string.IsNullOrEmpty(permission))
			{
				Log.Error("Permission checked was null.");
				__result = false;
				return false;
			}

			Log.Debug($"Permission string: {permission}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
			UserGroup userGroup = ServerStatic.GetPermissionsHandler().GetUserGroup(player.UserId);
			Exiled.Permissions.Features.Group group = null;

			if(userGroup != null)
			{
				Log.Debug($"UserGroup: {userGroup.BadgeText}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				string groupName = ServerStatic.GetPermissionsHandler()._groups.FirstOrDefault(g => g.Value == player.Group).Key;
				Log.Debug($"GroupName: {groupName}", Exiled.Loader.Loader.ShouldDebugBeShown);

				groupName = ServerStatic.GetPermissionsHandler()._members.FirstOrDefault(g => g.Key == player.UserId).Value;
				Log.Debug($"BadgeText:{player.Group.BadgeText} -> FixedGroupName: {groupName}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);

				if(Exiled.Permissions.Extensions.Permissions.Groups == null)
				{
					Log.Error("Permissions config is null.");
					__result = false;
					return false;
				}

				if(!Exiled.Permissions.Extensions.Permissions.Groups.Any())
				{
					Log.Error("No permission config groups.");
					__result = false;
					return false;
				}

				if(!Exiled.Permissions.Extensions.Permissions.Groups.TryGetValue(groupName, out group))
				{
					Log.Error("Could not get permission value.");
					__result = false;
					return false;
				}

				Log.Debug($"Got group.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
			}
			else
			{
				Log.Debug("Player group is null, getting default..", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				group = Exiled.Permissions.Extensions.Permissions.DefaultGroup;
			}

			if(group != null)
			{
				Log.Debug("Group is not null!", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				if(permission.Contains("."))
				{
					Log.Debug("Group contains permission separator", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
					if(group.Permissions.Any(s => s == ".*"))
					{
						Log.Debug("All permissions have been granted for all nodes.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
						__result = true;
						return false;
					}

					if(group.Permissions.Contains(permission.Split('.')[0] + ".*"))
					{
						Log.Debug("Check 1: True, returning.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
						__result = true;
						return false;
					}
				}

				if(group.Permissions.Contains(permission) || group.Permissions.Contains("*"))
				{
					Log.Debug("Check 2: True, returning.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
					__result = true;
					return false;
				}
			}
			else
			{
				Log.Debug("Group is null, returning false.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				__result = false;
				return false;
			}

			Log.Debug("No permissions found.", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
			__result = false;
			return false;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
	public static class IntercomTextPatch
	{
		public static void Prefix(Intercom __instance)
		{
			if(!SanyaPlugin.Instance.Config.IntercomInformation) return;

			int leftdecont = (int)((Math.Truncate((15f * 60) * 100f) / 100f) - (Math.Truncate(DecontaminationController.GetServerTime * 100f) / 100f));
			int leftautowarhead = AlphaWarheadController.Host != null ? (int)Mathf.Clamp(AlphaWarheadController.Host._autoDetonateTime - RoundSummary.roundTime, 0, AlphaWarheadController.Host._autoDetonateTime) : -1;
			int nextRespawn = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
			bool isContain = PlayerManager.localPlayer.GetComponent<CharacterClassManager>()._lureSpj.allowContain;
			bool isAlreadyUsed = UnityEngine.Object.FindObjectOfType<OneOhSixContainer>().used;

			float totalvoltagefloat = 0f;
			foreach(var i in Generator079.Generators)
			{
				totalvoltagefloat += i.localVoltage;
			}
			totalvoltagefloat *= 1000f;

			string contentfix = string.Concat(
				$"作戦経過時間 : {RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}\n",
				$"残存SCPオブジェクト : {RoundSummary.singleton.CountTeam(Team.SCP):00}/{RoundSummary.singleton.classlistStart.scps_except_zombies:00}\n",
				$"残存Dクラス職員 : {RoundSummary.singleton.CountTeam(Team.CDP):00}/{RoundSummary.singleton.classlistStart.class_ds:00}\n",
				$"残存研究員 : {RoundSummary.singleton.CountTeam(Team.RSC):00}/{RoundSummary.singleton.classlistStart.scientists:00}\n",
				$"施設内余剰電力 : {totalvoltagefloat:0000}kVA\n",
				$"AlphaWarheadのステータス : {(AlphaWarheadOutsitePanel.nukeside.enabled ? "READY" : "DISABLED")}\n",
				$"SCP-106再収用設備：{(isContain ? (isAlreadyUsed ? "使用済み" : "準備完了") : "人員不在")}\n",
				$"軽度収用区画閉鎖まで : {leftdecont / 60:00}:{leftdecont % 60:00}\n",
				$"自動施設爆破開始まで : {leftautowarhead / 60:00}:{leftautowarhead % 60:00}\n",
				$"接近中の部隊突入まで : {nextRespawn / 60:00}:{nextRespawn % 60:00}\n"
				);

			__instance.CustomContent = contentfix;

			return;
		}
	}

	//not override - 10.0.0 checked
	[HarmonyPatch(typeof(UnitNamingRule), nameof(UnitNamingRule.AddCombination))]
	public static class NTFUnitPatch
	{
		public static void Postfix(ref string regular)
		{
			if(PlayerManager.localPlayer == null || PlayerManager.localPlayer?.GetComponent<RandomSeedSync>().seed == 0) return;
			Log.Debug($"[NTFUnitPatch] unit:{regular}", SanyaPlugin.Instance.Config.IsDebugged);

			if(SanyaPlugin.Instance.Config.CassieSubtitle)
			{
				int SCPCount = 0;

				foreach(var i in Player.List)
					if(i.Team == Team.SCP && i.Role != RoleType.Scp0492)
						SCPCount++;

				if(SCPCount > 0)
					Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}", regular).Replace("{1}", SCPCount.ToString()), 30);
				else
					Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", regular), 30);
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(RespawnEffectsController), nameof(RespawnEffectsController.ServerExecuteEffects))]
	public static class RespawnEffectPatch
	{
		public static bool Prefix(RespawnEffectsController.EffectType type, SpawnableTeamType team)
		{
			Log.Debug($"[RespawnEffectPatch] {type}:{team}", SanyaPlugin.Instance.Config.IsDebugged);
			if(SanyaPlugin.Instance.Config.StopRespawnAfterDetonated && AlphaWarheadController.Host.detonated && type == RespawnEffectsController.EffectType.Selection) return false;
			else return true;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetPickup))]
	public static class ItemCleanupPatch
	{
		public static Dictionary<GameObject, float> items = new Dictionary<GameObject, float>();

		public static bool Prefix(Inventory __instance, ref Pickup __result, ItemType droppedItemId, float dur, Vector3 pos, Quaternion rot, int s, int b, int o)
		{
			if(SanyaPlugin.Instance.Config.ItemCleanup < 0 || __instance.name == "Host") return true;

			if(SanyaPlugin.Instance.Config.ItemCleanupIgnoreParsed.Contains(droppedItemId))
			{
				Log.Debug($"[ItemCleanupPatch] Ignored:{droppedItemId}", SanyaPlugin.Instance.Config.IsDebugged);
				return true;
			}

			Log.Debug($"[ItemCleanupPatch] {droppedItemId}{pos} Time:{Time.time} Cleanuptimes:{SanyaPlugin.Instance.Config.ItemCleanup}", SanyaPlugin.Instance.Config.IsDebugged);

			if(droppedItemId < ItemType.KeycardJanitor)
			{
				__result = null;
				return false;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(__instance.pickupPrefab);
			NetworkServer.Spawn(gameObject);
			items.Add(gameObject, Time.time);
			gameObject.GetComponent<Pickup>().SetupPickup(droppedItemId, dur, __instance.gameObject, new Pickup.WeaponModifiers(true, s, b, o), pos, rot);
			__result = gameObject.GetComponent<Pickup>();
			return false;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Grenade), nameof(Grenade.ServersideExplosion))]
	public static class GrenadeLogPatch
	{
		public static bool Prefix(Grenade __instance, ref bool __result)
		{
			try
			{
				if(__instance.thrower?.name != "Host")
				{
					string text = (__instance.thrower != null) ? (__instance.thrower.hub.characterClassManager.UserId + " (" + __instance.thrower.hub.nicknameSync.MyNick + ")") : "(UNKNOWN)";
					ServerLogs.AddLog(ServerLogs.Modules.Logger, "Player " + text + "'s " + __instance.logName + " grenade exploded.", ServerLogs.ServerLogType.GameEvent);
				}
				__result = true;
				return false;
			}
			catch(Exception)
			{
				return true;
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Scp018Grenade), nameof(Scp018Grenade.OnSpeedCollisionEnter))]
	public static class Scp018Patch
	{
		public static bool Prefix(Scp018Grenade __instance, Collision collision, float relativeSpeed)
		{
			Vector3 velocity = __instance.rb.velocity * __instance.bounceSpeedMultiplier;
			float num = __instance.topSpeedPerBounce[__instance.bounce];
			if(relativeSpeed > num)
			{
				__instance.rb.velocity = velocity.normalized * num;
				if(__instance.actionAllowed)
				{
					__instance.bounce = Mathf.Min(__instance.bounce + 1, __instance.topSpeedPerBounce.Length - 1);
				}
			}
			else
			{
				if(relativeSpeed > __instance.source.maxDistance)
				{
					__instance.source.maxDistance = relativeSpeed;
				}
				__instance.rb.velocity = velocity;
			}
			if(NetworkServer.active)
			{
				Collider collider = collision.collider;
				int num2 = 1 << collider.gameObject.layer;

				if(num2 == __instance.layerGlass)
				{
					if(__instance.actionAllowed && relativeSpeed >= __instance.breakpointGlass)
					{
						__instance.cooldown = __instance.cooldownGlass;
						BreakableWindow component = collider.GetComponent<BreakableWindow>();
						if(component != null)
						{
							component.ServerDamageWindow(relativeSpeed * __instance.damageGlass);
						}
					}
				}
				else if(num2 == __instance.layerDoor)
				{
					if(relativeSpeed >= __instance.breakpointDoor)
					{
						__instance.cooldown = __instance.cooldownDoor;
						Door componentInParent = collider.GetComponentInParent<Door>();
						if(componentInParent != null && !componentInParent.GrenadesResistant)
						{
							componentInParent.DestroyDoor(true);
						}
					}
				}
				else if((num2 == __instance.layerHitbox || num2 == __instance.layerIgnoreRaycast) && __instance.actionAllowed && relativeSpeed >= __instance.breakpointHurt)
				{
					__instance.cooldown = __instance.cooldownHurt;
					ReferenceHub componentInParent2 = collider.GetComponentInParent<ReferenceHub>();
					if(componentInParent2 != null && (ServerConsole.FriendlyFire || componentInParent2.gameObject == __instance.thrower.gameObject || componentInParent2.weaponManager.GetShootPermission(__instance.throwerTeam, false)))
					{
						float num3 = relativeSpeed * __instance.damageHurt * SanyaPlugin.Instance.Config.Scp018DamageMultiplier;

						//componentInParent2.playerStats.ccm.CurClass != RoleType.Scp106 && 
						if(componentInParent2.playerStats.ccm.Classes.SafeGet(componentInParent2.playerStats.ccm.CurClass).team == Team.SCP)
						{
							num3 *= __instance.damageScpMultiplier;
						}

						componentInParent2.playerStats.HurtPlayer(new PlayerStats.HitInfo(num3, __instance.logName, DamageTypes.Grenade, Player.Get(__instance.throwerGameObject).Id), componentInParent2.playerStats.gameObject);
					}
				}

				if(__instance.bounce >= __instance.topSpeedPerBounce.Length - 1 && relativeSpeed >= num && !__instance.hasHitMaxSpeed)
				{
					__instance.NetworkfuseTime = NetworkTime.time + 10.0;
					__instance.hasHitMaxSpeed = true;
				}
			}
			return false;
		}
	}

	//not override - 10.0.0 checked
	[HarmonyPatch(typeof(DissonanceUserSetup), nameof(DissonanceUserSetup.CallCmdAltIsActive))]
	public static class VCScpPatch
	{
		public static void Prefix(DissonanceUserSetup __instance, bool value)
		{
			if(__instance.gameObject.TryGetComponent<CharacterClassManager>(out CharacterClassManager ccm))
				if(ccm.IsAnyScp() && (ccm.CurClass.Is939() || SanyaPlugin.Instance.Config.AltvoicechatScpsParsed.Contains(ccm.CurClass)))
					__instance.MimicAs939 = value;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdSyncVoiceChatStatus))]
	public static class VCPreventsPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.Users != null && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(SanyaPlugin.Instance.Config.DisableAllChat) return false;
			if(!SanyaPlugin.Instance.Config.DisableSpectatorChat || (SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(__instance.ccm.UserId))) return true;
			var team = __instance.ccm.Classes.SafeGet(__instance.ccm.CurClass).team;
			Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance.isVoiceChatting} RoundEnded:{RoundSummary.singleton._roundEnded}", SanyaPlugin.Instance.Config.IsDebugged);
			if(SanyaPlugin.Instance.Config.DisableSpectatorChat && team == Team.RIP && !RoundSummary.singleton._roundEnded) b = false;
			return true;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdUpdateClass))]
	public static class VCTeamPatch
	{
		public static bool Prefix(Radio __instance)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.Users != null && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(!SanyaPlugin.Instance.Config.DisableAllChat) return true;
			Log.Debug($"[VCTeamPatch] {Player.Get(__instance.ccm.gameObject).Nickname} [{__instance.ccm.CurClass}]", SanyaPlugin.Instance.Config.IsDebugged);
			__instance._dissonanceSetup.TargetUpdateForTeam(Team.RIP);
			return false;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Scp079PlayerScript), nameof(Scp079PlayerScript.Start))]
	public static class Scp079ManaPatch
	{
		public static void Postfix(Scp079PlayerScript __instance)
		{
			foreach(Scp079PlayerScript.Ability079 ability in __instance.abilities)
			{
				switch(ability.label)
				{
					case "Camera Switch":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostCamera;
						break;
					case "Door Lock":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostLock;
						break;
					case "Door Lock Start":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostLockStart;
						break;
					case "Door Lock Minimum":
						ability.mana = SanyaPlugin.Instance.Config.Scp079ConstLockMinimum;
						break;
					case "Door Interaction DEFAULT":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorDefault;
						break;
					case "Door Interaction CONT_LVL_1":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorContlv1;
						break;
					case "Door Interaction CONT_LVL_2":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorContlv2;
						break;
					case "Door Interaction CONT_LVL_3":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorContlv3;
						break;
					case "Door Interaction ARMORY_LVL_1":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorArmlv1;
						break;
					case "Door Interaction ARMORY_LVL_2":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorArmlv2;
						break;
					case "Door Interaction ARMORY_LVL_3":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorArmlv3;
						break;
					case "Door Interaction EXIT_ACC":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorExit;
						break;
					case "Door Interaction INCOM_ACC":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorIntercom;
						break;
					case "Door Interaction CHCKPOINT_ACC":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostDoorCheckpoint;
						break;
					case "Room Lockdown":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostLockDown;
						break;
					case "Tesla Gate Burst":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostTesla;
						break;
					case "Elevator Teleport":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostElevatorTeleport;
						break;
					case "Elevator Use":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostElevatorUse;
						break;
					case "Speaker Start":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostSpeakerStart;
						break;
					case "Speaker Update":
						ability.mana = SanyaPlugin.Instance.Config.Scp079CostSpeakerUpdate;
						break;
				}
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Scp079PlayerScript), nameof(Scp079PlayerScript.CallCmdSwitchCamera))]
	public static class Scp079CameraPatch
	{
		public static bool Prefix(Scp079PlayerScript __instance, ref ushort cameraId, bool lookatRotation)
		{
			if(!SanyaPlugin.Instance.Config.Scp079ExtendEnabled) return true;

			Log.Debug($"[Scp079CameraPatch] {cameraId}:{lookatRotation}", SanyaPlugin.Instance.Config.IsDebugged);

			if(__instance.GetComponent<AnimationController>().curAnim != 1) return true;

			if(__instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelFindscp)
			{
				List<Camera079> cams = new List<Camera079>();
				foreach(var ply in Player.List)
					if(ply.Team == Team.SCP && ply.Role != RoleType.Scp079)
						cams.AddRange(ply.GetNearCams());

				Camera079 target;
				if(cams.Count > 0)
					target = cams.GetRandomOne();
				else return true;

				if(target != null)
				{
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp, __instance.curMana);
						return false;
					}

					__instance.RpcSwitchCamera(target.cameraId, lookatRotation);
					__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp;
					__instance.currentCamera = target;
					return false;
				}
			}
			return true;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Scp079PlayerScript), nameof(Scp079PlayerScript.CallCmdInteract))]
	public static class Scp079InteractPatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp079PlayerScript __instance, ref string command, ref GameObject target)
		{
			if(SanyaPlugin.Instance.Config.Scp079NeedInteractTierGateand914 < 0 && !SanyaPlugin.Instance.Config.Scp079ExtendEnabled) return true;

			var player = Player.Get(__instance.gameObject);
			Log.Debug($"[Scp079InteractPatch] {player.Nickname}({player.IsExmode()}) -> {command}", SanyaPlugin.Instance.Config.IsDebugged);

			if(command.Contains("DOOR:") 
				&& SanyaPlugin.Instance.Config.Scp079NeedInteractTierGateand914 > __instance.curLvl + 1 
				&& target.TryGetComponent<Door>(out var targetdoor)
				&& (targetdoor.PermissionLevels == Door.AccessRequirements.Gates || targetdoor.DoorName == "914"))
			{
				player.SendTextHint(HintTexts.Error079NotEnoughTier, 3);
				return false;
			}

			if(!player.IsExmode()) return true;

			string[] args = command.Split(':');

			if(command.Contains("SPEAKER:"))
			{
				string b = string.Empty;
				if(args.Length > 1)
				{
					b = args[1];
				}

				if(b == "HCZ_Nuke")
				{
					if(__instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelNuke)
					{
						if(SanyaPlugin.Instance.Config.Scp079ExtendCostNuke > __instance.curMana)
						{
							__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostNuke, __instance.curMana);
							return false;
						}

						if(!AlphaWarheadController.Host.inProgress)
						{
							AlphaWarheadController.Host.InstantPrepare();
							AlphaWarheadController.Host.StartDetonation();
							__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostNuke;
						}
						else
						{
							AlphaWarheadController.Host.CancelDetonation();
							__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostNuke;
						}
						return false;
					}
				}
			}
			else if(command.Contains("DOOR:"))
			{
				if(__instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelAirbomb && command.Contains("NukeSurface"))
				{
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostAirbomb > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostAirbomb, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.DoorName == "NUKE_SURFACE" && !Coroutines.isAirBombGoing && NineTailedFoxAnnouncer.singleton.Free)
					{
						SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb(limit: 5)));
						__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostAirbomb;
						return false;
					}
				}

				if(__instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelDoorbeep)
				{
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostDoorbeep > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostDoorbeep, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.curCooldown <= 0f)
					{
						player.ReferenceHub.playerInteract.RpcDenied(target);
						door.curCooldown = 0.5f;
						__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostDoorbeep;
					}
					return false;
				}
			}
			return true;
		}
	}

	//not override - for 10.0.0
	[HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.StartDetonation))]
	public static class AutoNukePatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static void Prefix(AlphaWarheadController __instance)
		{
			if(__instance._autoDetonate && __instance._autoDetonateTimer <= 0f)
			{
				__instance.InstantPrepare();
				__instance._autoDetonate = false;
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(RagdollManager), nameof(RagdollManager.SpawnRagdoll))]
	public static class PreventRagdollPatch
	{
		public static bool Prefix(RagdollManager __instance, PlayerStats.HitInfo ragdollInfo)
		{
			if(SanyaPlugin.Instance.Config.TeslaDeleteObjects && ragdollInfo.GetDamageType() == DamageTypes.Tesla) return false;
			else if(SanyaPlugin.Instance.Config.Scp939RemoveRagdoll && ragdollInfo.GetDamageType() == DamageTypes.Scp939) return false;
			else return true;
		}
	}

	//not override
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.AddVision))]
	public static class Scp939VisionShieldPatch
	{
		public static void Prefix(Scp939_VisionController __instance, Scp939PlayerScript scp939)
		{
			if(SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount < 0 || __instance._ccm.CurRole.team == Team.SCP) return;
			bool isFound = false;
			for(int i = 0; i < __instance.seeingSCPs.Count; i++)
			{
				if(__instance.seeingSCPs[i].scp == scp939)
				{
					isFound = true;
				}
			}
			
			if(!isFound)
			{
				Log.Debug($"[Scp939VisionShieldPatch] {scp939._hub.nicknameSync.MyNick}({scp939._hub.characterClassManager.CurClass}) -> {__instance._ccm._hub.nicknameSync.MyNick}({__instance._ccm.CurClass})", SanyaPlugin.Instance.Config.IsDebugged);
				scp939._hub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount;
				scp939._hub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(scp939._hub.playerStats.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, scp939._hub.playerStats.maxArtificialHealth);
			}

		}
	}

	//override
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.UpdateVisions))]
	public static class Scp939VisionShieldRemovePatch
	{
		public static bool Prefix(Scp939_VisionController __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount < 0) return true;

			for(int i = 0; i < __instance.seeingSCPs.Count; i++)
			{
				__instance.seeingSCPs[i].remainingTime -= 0.02f;
				if(__instance.seeingSCPs[i].scp == null || !__instance.seeingSCPs[i].scp.iAm939 || __instance.seeingSCPs[i].remainingTime <= 0f)
				{
					if(__instance.seeingSCPs[i].scp != null && __instance.seeingSCPs[i].scp.iAm939)
					{
						Log.Debug($"[Scp939VisionShieldRemovePatch] {__instance._ccm._hub.nicknameSync.MyNick}({__instance._ccm.CurClass})", SanyaPlugin.Instance.Config.IsDebugged);
						__instance.seeingSCPs[i].scp._hub.playerStats.NetworkmaxArtificialHealth = Mathf.Clamp(__instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth - SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, __instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth);
						__instance.seeingSCPs[i].scp._hub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(__instance.seeingSCPs[i].scp._hub.playerStats.unsyncedArtificialHealth - SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, __instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth);
					}
					__instance.seeingSCPs.RemoveAt(i);
					return false;
				}
			}
			return false;
		}
	}

	//transpiler
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.MaxShield), MethodType.Getter)]
	public static class Scp096InitPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach(var code in instructions)
			{
				if(code.opcode == OpCodes.Ldc_R4) code.operand = (float)SanyaPlugin.Instance.Config.Scp096InitialShield;
				yield return code;
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.AdjustShield))]
	public static class Scp096ShieldPatch
	{
		public static void Prefix(PlayableScps.Scp096 __instance, ref int amt)
		{
			amt = SanyaPlugin.Instance.Config.Scp096ShieldPerTargets;
		}
	}

	//not override
	[HarmonyPatch(typeof(CharacterClassManager), nameof(CharacterClassManager.Start))]
	public static class InitHPPatch
	{
		public static void Prefix(CharacterClassManager __instance)
		{
			foreach(var role in __instance.Classes)
			{
				switch(role.roleId)
				{
					case RoleType.Scp173:
						role.maxHP = SanyaPlugin.Instance.Config.Scp173MaxHp;
						break;
					case RoleType.Scp106:
						role.maxHP = SanyaPlugin.Instance.Config.Scp106MaxHp;
						break;
					case RoleType.Scp049:
						role.maxHP = SanyaPlugin.Instance.Config.Scp049MaxHp;
						break;
					case RoleType.Scp096:
						role.maxHP = SanyaPlugin.Instance.Config.Scp096MaxHp;
						break;
					case RoleType.Scp93953:
					case RoleType.Scp93989:
						role.maxHP = SanyaPlugin.Instance.Config.Scp939MaxHp;
						break;
				}
			}
		}
	}
}