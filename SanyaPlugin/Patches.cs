﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using MEC;
using Grenades;
using Security;
using LightContainmentZoneDecontamination;
using Exiled.API.Features;
using HarmonyLib;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;


namespace SanyaPlugin.Patches
{
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
			if(!SanyaPlugin.instance.Config.IntercomInformation) return true;

			int leftdecont = (int)((Math.Truncate((15f * 60) * 100f) / 100f) - (Math.Truncate(DecontaminationController.GetServerTime * 100f) / 100f));
			int leftautowarhead = AlphaWarheadController.Host != null ? (int)Mathf.Clamp(AlphaWarheadController.Host._autoDetonateTime - RoundSummary.roundTime, 0, AlphaWarheadController.Host._autoDetonateTime) : -1;
			int nextRespawn = (int)Math.Truncate(PlayerManager.localPlayer.GetComponent<MTFRespawn>().timeToNextRespawn + PlayerManager.localPlayer.GetComponent<MTFRespawn>().respawnCooldown);
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
					__instance._content = contentfix + $"{Player.Get(__instance.speaker).Nickname}が放送中... : 残り" + Mathf.CeilToInt(__instance.speechRemainingTime) + "秒";
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

			if(SanyaPlugin.instance.Config.CassieSubtitle)
			{
				int SCPCount = 0;

				foreach(var i in Player.List)
					if(i.Team == Team.SCP && i.Role != RoleType.Scp0492)
						SCPCount++;

				if(SCPCount > 0)
					Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}", unit).Replace("{1}", SCPCount.ToString()), 30);
				else
					Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", unit), 30);
			}
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(MTFRespawn), nameof(MTFRespawn.SummonChopper))]
	public static class StopChopperAfterDetonatedPatch
	{
		public static bool Prefix()
		{
			if(SanyaPlugin.instance.Config.StopRespawnAfterDetonated && AlphaWarheadController.Host.detonated) return false;
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
			if(SanyaPlugin.instance.Config.ItemCleanup < 0 || __instance.name == "Host") return true;

			if(SanyaPlugin.instance.Config.ItemCleanupIgnoreParsed.Contains(droppedItemId))
			{
				Log.Debug($"[ItemCleanupPatch] Ignored:{droppedItemId}");
				return true;
			}

			Log.Debug($"[ItemCleanupPatch] {droppedItemId}{pos} Time:{Time.time} Cleanuptimes:{SanyaPlugin.instance.Config.ItemCleanup}");

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
			if(SanyaPlugin.instance.Config.Scp018FriendlyFire && __instance is Scp018Grenade) value = Team.TUT;
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

				if(!SanyaPlugin.instance.Config.Scp018CantDestroyObject)
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
						float num3 = relativeSpeed * __instance.damageHurt * SanyaPlugin.instance.Config.Scp018DamageMultiplier;

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
			//__instance.OnSpeedCollisionEnter(collision, relativeSpeed);
			return false;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdSyncVoiceChatStatus))]
	public static class VCPreventsPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(SanyaPlugin.instance.Config.DisableChatBypassWhitelist && WhiteList.Users != null && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(SanyaPlugin.instance.Config.DisableAllChat) return false;
			if(!SanyaPlugin.instance.Config.DisableSpectatorChat || (SanyaPlugin.instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(__instance.ccm.UserId))) return true;
			var team = __instance.ccm.Classes.SafeGet(__instance.ccm.CurClass).team;
			Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance.isVoiceChatting} RoundEnded:{RoundSummary.singleton._roundEnded}");
			if(SanyaPlugin.instance.Config.DisableSpectatorChat && team == Team.RIP && !RoundSummary.singleton._roundEnded) b = false;
			return true;
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdUpdateClass))]
	public static class VCTeamPatch
	{
		public static bool Prefix(Radio __instance)
		{
			if(SanyaPlugin.instance.Config.DisableChatBypassWhitelist && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.Users != null && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(!SanyaPlugin.instance.Config.DisableAllChat) return true;
			Log.Debug($"[VCTeamPatch] {Player.Get(__instance.ccm.gameObject).Nickname} [{__instance.ccm.CurClass}]");
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
						ability.mana = SanyaPlugin.instance.Config.Scp079CostCamera;
						break;
					case "Door Lock":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostLock;
						break;
					case "Door Lock Start":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostLockStart;
						break;
					case "Door Lock Minimum":
						ability.mana = SanyaPlugin.instance.Config.Scp079ConstLockMinimum;
						break;
					case "Door Interaction DEFAULT":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorDefault;
						break;
					case "Door Interaction CONT_LVL_1":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorContlv1;
						break;
					case "Door Interaction CONT_LVL_2":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorContlv2;
						break;
					case "Door Interaction CONT_LVL_3":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorContlv3;
						break;
					case "Door Interaction ARMORY_LVL_1":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorArmlv1;
						break;
					case "Door Interaction ARMORY_LVL_2":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorArmlv2;
						break;
					case "Door Interaction ARMORY_LVL_3":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorArmlv3;
						break;
					case "Door Interaction EXIT_ACC":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorExit;
						break;
					case "Door Interaction INCOM_ACC":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorIntercom;
						break;
					case "Door Interaction CHCKPOINT_ACC":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostDoorCheckpoint;
						break;
					case "Room Lockdown":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostLockDown;
						break;
					case "Tesla Gate Burst":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostTesla;
						break;
					case "Elevator Teleport":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostElevatorTeleport;
						break;
					case "Elevator Use":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostElevatorUse;
						break;
					case "Speaker Start":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostSpeakerStart;
						break;
					case "Speaker Update":
						ability.mana = SanyaPlugin.instance.Config.Scp079CostSpeakerUpdate;
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
			if(!SanyaPlugin.instance.Config.Scp079ExtendEnabled) return true;

			Log.Debug($"[Scp079CameraPatch] {cameraId}:{lookatRotation}");

			if(__instance.GetComponent<AnimationController>().curAnim != 1) return true;

			if(__instance.curLvl + 1 >= SanyaPlugin.instance.Config.Scp079ExtendLevelFindscp)
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
					if(SanyaPlugin.instance.Config.Scp079ExtendCostFindscp > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.instance.Config.Scp079ExtendCostFindscp, __instance.curMana);
						return false;
					}

					__instance.RpcSwitchCamera(target.cameraId, lookatRotation);
					__instance.Mana -= SanyaPlugin.instance.Config.Scp079ExtendCostFindscp;
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
			if(!SanyaPlugin.instance.Config.Scp079ExtendEnabled) return true;

			var player = Player.Get(__instance.gameObject);
			Log.Debug($"[Scp079InteractPatch] {player.Nickname}({player.IsExmode()}) -> {command}");

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
					if(__instance.curLvl + 1 >= SanyaPlugin.instance.Config.Scp079ExtendLevelNuke)
					{
						if(SanyaPlugin.instance.Config.Scp079ExtendCostNuke > __instance.curMana)
						{
							__instance.RpcNotEnoughMana(SanyaPlugin.instance.Config.Scp079ExtendCostNuke, __instance.curMana);
							return false;
						}

						if(!AlphaWarheadController.Host.inProgress)
						{
							AlphaWarheadController.Host.InstantPrepare();
							AlphaWarheadController.Host.StartDetonation();
							__instance.Mana -= SanyaPlugin.instance.Config.Scp079ExtendCostNuke;
						}
						else
						{
							AlphaWarheadController.Host.CancelDetonation();
							__instance.Mana -= SanyaPlugin.instance.Config.Scp079ExtendCostNuke;
						}
						return false;
					}
				}
			}
			else if(command.Contains("DOOR:"))
			{
				if(__instance.curLvl + 1 >= SanyaPlugin.instance.Config.Scp079ExtendLevelAirbomb && command.Contains("NukeSurface"))
				{
					if(SanyaPlugin.instance.Config.Scp079ExtendCostAirbomb > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.instance.Config.Scp079ExtendCostAirbomb, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.DoorName == "NUKE_SURFACE" && !Coroutines.isAirBombGoing && NineTailedFoxAnnouncer.singleton.Free)
					{
						SanyaPlugin.instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb(limit: 5)));
						__instance.Mana -= SanyaPlugin.instance.Config.Scp079ExtendCostAirbomb;
						return false;
					}
				}

				if(__instance.curLvl + 1 >= SanyaPlugin.instance.Config.Scp079ExtendLevelDoorbeep)
				{
					if(SanyaPlugin.instance.Config.Scp079ExtendCostDoorbeep > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.instance.Config.Scp079ExtendCostDoorbeep, __instance.curMana);
						return false;
					}

					var door = target.GetComponent<Door>();
					if(door != null && door.curCooldown <= 0f)
					{
						player.ReferenceHub.playerInteract.RpcDenied(target);
						door.curCooldown = 0.5f;
						__instance.Mana -= SanyaPlugin.instance.Config.Scp079ExtendCostDoorbeep;
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
			Log.Warn($"[AntiCheatKill] {Player.Get(__instance.gameObject).Nickname} detect AntiCheat:{message}");
			if(SanyaPlugin.instance.Config.AnticheatKillDisable)
				return false;
			else
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
			if(AlphaWarheadController.Host._autoDetonateTimer <= 0f)
			{
				__instance.InstantPrepare();
				AlphaWarheadController.Host._autoDetonate = false;
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(RagdollManager), nameof(RagdollManager.SpawnRagdoll))]
	public static class PreventRagdollPatch
	{
		public static bool Prefix(RagdollManager __instance, PlayerStats.HitInfo ragdollInfo)
		{
			if(SanyaPlugin.instance.Config.TeslaDeleteRagdolls && ragdollInfo.GetDamageType() == DamageTypes.Tesla) return false;
			else if(SanyaPlugin.instance.Config.Scp939RemoveRagdoll && ragdollInfo.GetDamageType() == DamageTypes.Scp939) return false;
			else return true;
		}
	}
}