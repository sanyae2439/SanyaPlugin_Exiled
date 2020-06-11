using System;
using System.Collections.Generic;
using System.Linq;
using EXILED;
using EXILED.Extensions;
using Grenades;
using Harmony;
using MEC;
using Mirror;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using Security;
using UnityEngine;
using CustomPlayerEffects;

namespace SanyaPlugin.Patches
{
	//override - 10.0.0 checked
	[HarmonyPatch(typeof(PermissionPlugin), nameof(PermissionPlugin.CheckPermission))]
	public static class EXILEDPermissionOnLevelPatch
	{
		public static bool Prefix(ref bool __result, ReferenceHub player, string permission)
		{
			if(!Configs.level_enabled) return true;
			Log.Debug($"[EXILEDPermissionOnLevelPatch] Overrided.");

			if(player == null)
			{
				Log.Error("Reference hub was null, unable to check permissions.");
				__result = false;
				return false;
			}

			Log.Debug($"Player: {player.GetNickname()} UserID: {player.GetUserId()}");
			if(string.IsNullOrEmpty(permission))
			{
				Log.Error("Permission checked was null.");
				__result = false;
				return false;
			}

			Log.Debug($"Permission string: {permission}");
			UserGroup userGroup = ServerStatic.GetPermissionsHandler().GetUserGroup(player.GetUserId());
			PermissionPlugin.Group group = null;
			if(userGroup != null)
			{
				Log.Debug($"UserGroup: {userGroup.BadgeText}");
				string groupName = ServerStatic.GetPermissionsHandler()._groups.FirstOrDefault(g => g.Value == player.serverRoles.Group).Key;
				Log.Debug($"GroupName: {groupName}");

				groupName = ServerStatic.GetPermissionsHandler()._members.FirstOrDefault(g => g.Key == player.GetUserId()).Value;
				Log.Debug($"BadgeText:{player.serverRoles.Group.BadgeText} -> FixedGroupName:{groupName}");


				if(PermissionPlugin.permissionsconfig == null)
				{
					Log.Error("Permissions config is null.");
					__result = false;
					return false;
				}

				if(!PermissionPlugin.permissionsconfig.groups.Any())
				{
					Log.Error("No permissionconfig groups.");
					__result = false;
					return false;
				}

				if(!PermissionPlugin.permissionsconfig.groups.TryGetValue(groupName, out group))
				{
					Log.Error("Could not get permission value.");
					__result = false;
					return false;
				}
				Log.Debug($"Got group.");
			}
			else
			{
				Log.Debug("user group is null, getting default..");
				group = PermissionPlugin.GetDefaultGroup();
			}

			if(group != null)
			{
				Log.Debug("Group is not null!");
				if(permission.Contains("."))
				{
					Log.Debug("Group contains perm seperator");
					if(group.permissions.Any(s => s == ".*"))
					{
						Log.Debug("All perms granted for all nodes.");
						__result = true;
						return false;
					}
					if(group.permissions.Contains(permission.Split('.')[0] + ".*"))
					{
						Log.Debug("Check 1: True, returning.");
						__result = true;
						return false;
					}
				}

				if(group.permissions.Contains(permission) || group.permissions.Contains("*"))
				{
					Log.Debug("Check 2: True, returning.");
					__result = true;
					return false;
				}
			}
			else
			{
				Log.Debug("Group is null, returning false.");
				__result = false;
				return false;
			}
			Log.Debug("No permissions found.");
			__result = false;
			return false;
		}
	}

	//not override - 10.0.0 checked - DEBUG
	[HarmonyPatch(typeof(RateLimit), nameof(RateLimit.CanExecute))]
	public static class RateLimitPatch
	{
		public static void Postfix(RateLimit __instance, ref bool __result)
		{
			if(__result == false)
			{
				Log.Debug($"[RateLimitPatch] {__instance._usagesAllowed}:{__instance._timeWindow}");
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
	public static class IntercomTextPatch
	{
		public static bool Prefix(Intercom __instance)
		{
			if(!Configs.intercom_information) return true;

			//int leftdecont = (int)((Math.Truncate(((11.74f * 60) * 100f)) / 100f) - (Math.Truncate(PlayerManager.localPlayer.GetComponent<DecontaminationLCZ>().time * 100f) / 100f));
			int leftdecont = -1;
			int leftautowarhead = AlphaWarheadController.Host != null ? (int)Mathf.Clamp(AlphaWarheadController.Host._autoDetonateTime - RoundSummary.roundTime, 0, AlphaWarheadController.Host._autoDetonateTime) : -1;
			int nextRespawn = (int)Math.Truncate(PlayerManager.localPlayer.GetComponent<MTFRespawn>().timeToNextRespawn + PlayerManager.localPlayer.GetComponent<MTFRespawn>().respawnCooldown);
			bool isContain = PlayerManager.localPlayer.GetComponent<CharacterClassManager>()._lureSpj.allowContain;
			bool isAlreadyUsed = UnityEngine.Object.FindObjectOfType<OneOhSixContainer>().used;

			float totalvoltagefloat = 0f;
			foreach(var i in Generator079.generators)
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

			if(__instance.Muted)
			{
				__instance._content = contentfix + "アクセスが拒否されました";
			}
			else if(Intercom.AdminSpeaking)
			{
				__instance._content = contentfix + "管理者が放送設備をオーバーライド中";
			}
			else if(__instance.remainingCooldown > 0f)
			{
				__instance._content = contentfix + "放送設備再起動中 : " + Mathf.CeilToInt(__instance.remainingCooldown) + "秒必要";
			}
			else if(__instance.speaker != null)
			{
				if(__instance.speechRemainingTime == -77f)
				{
					__instance._content = contentfix + "放送中... : オーバーライド";
				}
				else
				{
					__instance._content = contentfix + $"{ReferenceHub.GetHub(__instance.speaker).GetNickname()}が放送中... : 残り" + Mathf.CeilToInt(__instance.speechRemainingTime) + "秒";
				}
			}
			else
			{
				__instance._content = contentfix + "放送設備準備完了";
			}

			if(__instance._contentDirty)
			{
				__instance.NetworkintercomText = __instance._content;
				__instance._contentDirty = false;
			}
			if(Intercom.AdminSpeaking != Intercom.LastState)
			{
				Intercom.LastState = Intercom.AdminSpeaking;
				__instance.RpcUpdateAdminStatus(Intercom.AdminSpeaking);
			}

			return false;
		}
	}

	//not override - 10.0.0 checked
	[HarmonyPatch(typeof(NineTailedFoxUnits), nameof(NineTailedFoxUnits.AddUnit))]
	public static class NTFUnitPatch
	{
		public static void Postfix(ref string unit)
		{
			if(PlayerManager.localPlayer == null || PlayerManager.localPlayer?.GetComponent<RandomSeedSync>().seed == 0) return;
			Log.Debug($"[NTFUnitPatch] unit:{unit}");

			if(Configs.cassie_subtitle)
			{
				int SCPCount = 0;
				foreach(var i in Player.GetHubs())
				{
					if(i.GetTeam() == Team.SCP && i.GetRole() != RoleType.Scp0492)
					{
						SCPCount++;
					}
				}

				if(SCPCount > 0)
				{
					Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}", unit).Replace("{1}", SCPCount.ToString()), 30);
				}
				else
				{
					Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", unit), 30);
				}
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(MTFRespawn), nameof(MTFRespawn.SummonChopper))]
	public static class StopChopperAfterDetonatedPatch
	{
		public static bool Prefix()
		{
			if(Configs.stop_respawn_after_detonated && AlphaWarheadController.Host.detonated) return false;
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
			if(Configs.item_cleanup < 0 || __instance.name == "Host") return true;

			Log.Debug($"[ItemCleanupPatch] {droppedItemId}{pos} Time:{Time.time} Cleanuptimes:{Configs.item_cleanup}");
			if(Configs.item_cleanup_ignore.Contains(droppedItemId))
			{
				Log.Debug($"[ItemCleanupPatch] Ignored:{droppedItemId}");
				return true;
			}

			if(droppedItemId < ItemType.KeycardJanitor)
			{
				__result = null;
				return false;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(__instance.pickupPrefab);
			NetworkServer.Spawn(gameObject);
			items.Add(gameObject, Time.time);
			gameObject.GetComponent<Pickup>().SetupPickup(new Pickup.PickupInfo
			{
				itemId = droppedItemId,
				durability = dur,
				weaponMods = new int[]
				{
				s,
				b,
				o
				},
				ownerPlayer = __instance.gameObject
			}, pos, rot);
			__result = gameObject.GetComponent<Pickup>();
			return false;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Grenade), nameof(Grenade.NetworkthrowerTeam), MethodType.Setter)]
	public static class GrenadeThrowerPatch
	{
		public static void Prefix(Grenade __instance, ref Team value)
		{
			Log.Debug($"[GrenadeThrowerPatch] value:{value} isscp018:{__instance is Scp018Grenade}");
			if(Configs.scp018_friendly_fire && __instance is Scp018Grenade) value = Team.TUT;
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

				if(!Configs.scp018_cant_destory_object)
				{
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
								componentInParent.DestroyDoor(b: true);
							}
						}
					}
				}

				if((num2 == __instance.layerHitbox || num2 == __instance.layerIgnoreRaycast) && __instance.actionAllowed && relativeSpeed >= __instance.breakpointHurt)
				{
					__instance.cooldown = __instance.cooldownHurt;
					ReferenceHub componentInParent2 = collider.GetComponentInParent<ReferenceHub>();
					if(componentInParent2 != null && (ServerConsole.FriendlyFire || componentInParent2.gameObject == __instance.thrower.gameObject || componentInParent2.weaponManager.GetShootPermission(__instance.throwerTeam)))
					{
						float num3 = relativeSpeed * __instance.damageHurt * Configs.scp018_damage_multiplier;

						//componentInParent2.playerStats.ccm.CurClass != RoleType.Scp106 && 
						if(componentInParent2.playerStats.ccm.Classes.SafeGet(componentInParent2.playerStats.ccm.CurClass).team == Team.SCP)
						{
							num3 *= __instance.damageScpMultiplier;
						}

						componentInParent2.playerStats.HurtPlayer(new PlayerStats.HitInfo(num3, __instance.logName, DamageTypes.Grenade, __instance.throwerGameObject.GetPlayer().GetPlayerId()), componentInParent2.playerStats.gameObject);
					}
				}

				if(__instance.bounce >= __instance.topSpeedPerBounce.Length - 1 && relativeSpeed >= num && !__instance.hasHitMaxSpeed)
				{
					__instance.NetworkfuseTime = NetworkTime.time + 10.0;
					__instance.hasHitMaxSpeed = true;
				}
			}
			//__instance.OnSpeedCollisionEnter(collision, relativeSpeed);
			return false;
		}
	}

	//not override - 10.0.0 checked
	[HarmonyPatch(typeof(CheaterReport), nameof(CheaterReport.CallCmdReport))]
	public static class ReportPatch
	{
		public static void Prefix(CheaterReport __instance, int playerId, string reason, bool notifyGm)
		{
			ReferenceHub reported = Player.GetPlayer(playerId);
			ReferenceHub reporter = Player.GetPlayer(__instance.gameObject);
			Log.Debug($"[ReportPatch] Reported:{reported.GetNickname()} Reason:{reason} Reporter:{reporter.GetNickname()}");

			if(!string.IsNullOrEmpty(Configs.report_webhook)
				&& !string.IsNullOrEmpty(reporter.GetUserId())
				&& !string.IsNullOrEmpty(reported.GetUserId())
				&& reported.GetPlayerId() != reporter.GetPlayerId()
				&& !notifyGm)
			{
				Log.Warn($"[Report] {reporter.GetNickname()} -> {reported.GetNickname()} Reason:{reason}");
				Methods.SendReport(reported, reason, reporter);
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdSyncVoiceChatStatus))]
	public static class VCPreventsPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(Configs.disable_chat_bypass_whitelist && WhiteList.Users != null && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(Configs.disable_all_chat) return false;
			if(!Configs.disable_spectator_chat || (Configs.disable_chat_bypass_whitelist && WhiteList.IsOnWhitelist(__instance.ccm.UserId))) return true;
			var team = __instance.ccm.Classes.SafeGet(__instance.ccm.CurClass).team;
			Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance.isVoiceChatting} RoundEnded:{RoundSummary.singleton.roundEnded}");
			if(Configs.disable_spectator_chat && team == Team.RIP && !RoundSummary.singleton.roundEnded) b = false;
			return true;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdUpdateClass))]
	public static class VCTeamPatch
	{
		public static bool Prefix(Radio __instance)
		{
			if(Configs.disable_chat_bypass_whitelist && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.Users != null && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(!Configs.disable_all_chat) return true;
			Log.Debug($"[VCTeamPatch] {__instance.ccm.gameObject.GetPlayer().GetNickname()} [{__instance.ccm.CurClass}]");
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
						ability.mana = Configs.scp079_cost_camera;
						break;
					case "Door Lock":
						ability.mana = Configs.scp079_cost_lock;
						break;
					case "Door Lock Start":
						ability.mana = Configs.scp079_cost_lock_start;
						break;
					case "Door Lock Minimum":
						ability.mana = Configs.scp079_cost_lock_minimum;
						break;
					case "Door Interaction DEFAULT":
						ability.mana = Configs.scp079_cost_door_default;
						break;
					case "Door Interaction CONT_LVL_1":
						ability.mana = Configs.scp079_cost_door_contlv1;
						break;
					case "Door Interaction CONT_LVL_2":
						ability.mana = Configs.scp079_cost_door_contlv2;
						break;
					case "Door Interaction CONT_LVL_3":
						ability.mana = Configs.scp079_cost_door_contlv3;
						break;
					case "Door Interaction ARMORY_LVL_1":
						ability.mana = Configs.scp079_cost_door_armlv1;
						break;
					case "Door Interaction ARMORY_LVL_2":
						ability.mana = Configs.scp079_cost_door_armlv2;
						break;
					case "Door Interaction ARMORY_LVL_3":
						ability.mana = Configs.scp079_cost_door_armlv3;
						break;
					case "Door Interaction EXIT_ACC":
						ability.mana = Configs.scp079_cost_door_exit;
						break;
					case "Door Interaction INCOM_ACC":
						ability.mana = Configs.scp079_cost_door_intercom;
						break;
					case "Door Interaction CHCKPOINT_ACC":
						ability.mana = Configs.scp079_cost_door_checkpoint;
						break;
					case "Room Lockdown":
						ability.mana = Configs.scp079_cost_lockdown;
						break;
					case "Tesla Gate Burst":
						ability.mana = Configs.scp079_cost_tesla;
						break;
					case "Elevator Teleport":
						ability.mana = Configs.scp079_cost_elevator_teleport;
						break;
					case "Elevator Use":
						ability.mana = Configs.scp079_cost_elevator_use;
						break;
					case "Speaker Start":
						ability.mana = Configs.scp079_cost_speaker_start;
						break;
					case "Speaker Update":
						ability.mana = Configs.scp079_cost_speaker_update;
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
			if(!Configs.scp079_ex_enabled) return true;

			Log.Debug($"[Scp079CameraPatch] {cameraId}:{lookatRotation}");

			if(__instance.GetComponent<AnimationController>().curAnim != 1) return true;

			if(__instance.curLvl + 1 >= Configs.scp079_ex_level_findscp)
			{
				List<Camera079> cams = new List<Camera079>();
				foreach(var ply in Player.GetHubs())
				{
					if(ply.GetTeam() == Team.SCP && ply.GetRole() != RoleType.Scp079)
					{
						cams.AddRange(ply.GetNearCams());
					}
				}

				Camera079 target;
				if(cams.Count > 0)
				{
					target = cams.GetRandomOne();
				}
				else return true;

				if(target != null)
				{
					if(Configs.scp079_ex_cost_findscp > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(Configs.scp079_ex_cost_findscp, __instance.curMana);
						return false;
					}

					__instance.RpcSwitchCamera(target.cameraId, lookatRotation);
					__instance.Mana -= Configs.scp079_ex_cost_findscp;
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
		public static bool Prefix(Scp079PlayerScript __instance, ref string command, ref GameObject target)
		{
			if(!Configs.scp079_ex_enabled) return true;

			var player = __instance.gameObject.GetPlayer();
			Log.Debug($"[Scp079InteractPatch] {player.animationController.curAnim} -> {command}");

			if(player.animationController.curAnim != 1) return true;

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
					if(__instance.curLvl + 1 >= Configs.scp079_ex_level_nuke)
					{
						if(Configs.scp079_ex_cost_nuke > __instance.curMana)
						{
							__instance.RpcNotEnoughMana(Configs.scp079_ex_cost_nuke, __instance.curMana);
							return false;
						}

						if(!AlphaWarheadController.Host.inProgress)
						{
							AlphaWarheadController.Host.InstantPrepare();
							AlphaWarheadController.Host.StartDetonation();
							__instance.Mana -= Configs.scp079_ex_cost_nuke;
						}
						else
						{
							AlphaWarheadController.Host.CancelDetonation();
							__instance.Mana -= Configs.scp079_ex_cost_nuke;
						}
						return false;
					}
				}
			}
			else if(command.Contains("DOOR:"))
			{

				if(__instance.curLvl + 1 >= Configs.scp079_ex_level_airbomb && command.Contains("NukeSurface"))
				{
					if(Configs.scp079_ex_cost_airbomb > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(Configs.scp079_ex_cost_airbomb, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.DoorName == "NUKE_SURFACE" && !Coroutines.isAirBombGoing && NineTailedFoxAnnouncer.singleton.Free)
					{
						SanyaPlugin.instance.EventHandlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb(limit: 5)));
						__instance.Mana -= Configs.scp079_ex_cost_airbomb;
						return false;
					}
				}

				if(__instance.curLvl + 1 >= Configs.scp079_ex_level_doorbeep)
				{
					if(Configs.scp079_ex_cost_doorbeep > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(Configs.scp079_ex_cost_doorbeep, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.curCooldown <= 0f)
					{
						player.playerInteract.RpcDenied(target);
						door.curCooldown = 0.5f;
						__instance.Mana -= Configs.scp079_ex_cost_doorbeep;
					}
					return false;
				}
			}
			return true;
		}
	}

	//override - for 10.0.0
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.AntiCheatKillPlayer))]
	public static class AntiCheatKillDisablePatch
	{
		public static bool Prefix(PlayerMovementSync __instance, string message)
		{
			Log.Warn($"[AntiCheatKill] {__instance._hub.GetNickname()} detect AntiCheat:{message}");
			if(Configs.beta_anticheat_disable)
				return false;
			else
				return true;
		}
	}

	//override - test - for 1.0.0
	[HarmonyPatch(typeof(PlayerEffectsController), nameof(PlayerEffectsController.Resync))]
	public static class EffectControllerPatch
	{
		public static bool Prefix(PlayerEffectsController __instance)
		{
			if(!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void PlayerEffectsController::Resync()' called on client");
				return false;
			}
			if(__instance.AllEffects.Count != __instance.syncEffectsIntensity.Count)
			{
				__instance.syncEffectsIntensity.Clear();
				foreach(PlayerEffect playerEffect in __instance.AllEffects.Values)
				{
					__instance.syncEffectsIntensity.Add(playerEffect.Intensity);
				}
				//return;
			}
			for(int i = 0; i < __instance.AllEffects.Count; i++)
			{
				KeyValuePair<Type, PlayerEffect> effectFromIndex = __instance.GetEffectFromIndex(i);
				if(effectFromIndex.Value.Intensity != __instance.syncEffectsIntensity[i])
				{
					__instance.syncEffectsIntensity[i] = effectFromIndex.Value.Intensity;
				}
			}
			return false;
		}
	}

	//override - for 10.0.0
	[HarmonyPatch(typeof(AnnounceDecontaminationEvent), nameof(AnnounceDecontaminationEvent.AnnouncementId), MethodType.Setter)]
	public static class EXILEDAnnounceDecontaminationEvent
	{
		public static bool Prefix(AnnounceDecontaminationEvent __instance, ref int value)
		{
			AccessTools.Field(typeof(AnnounceDecontaminationEvent), "announcementId").SetValue(__instance, Mathf.Clamp(value, 0, 6));
			return false;
		}
	}

	//not override - for 10.0.0
	[HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.StartDetonation))]
	public static class AutoNukePatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static void Prefix(AlphaWarheadController __instance)
		{
			if(__instance._autoDetonateTimer <= 0f)
			{
				__instance.InstantPrepare();
				__instance._autoDetonate = false;
			}
		}
	}
}