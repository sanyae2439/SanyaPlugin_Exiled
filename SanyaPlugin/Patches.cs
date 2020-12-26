using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Assets._Scripts.Dissonance;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Grenades;
using HarmonyLib;
using Hints;
using LightContainmentZoneDecontamination;
using MEC;
using Mirror;
using NorthwoodLib.Pools;
using Respawning;
using Respawning.NamingRules;
using sanyae2439.SyncVarHackExtensions;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	//override - Exiled 2.0
	[HarmonyPatch(typeof(Permissions), nameof(Permissions.CheckPermission), new Type[] { typeof(Player), typeof(string) })]
	public static class ExiledPermissionPatcher
	{
		public static bool Prefix(Player player, string permission, ref bool __result)
		{
			if(string.IsNullOrEmpty(permission))
			{
				__result = false;
				return false;
			}

			if(player == null || player.GameObject == null || Permissions.Groups == null || Permissions.Groups.Count == 0)
			{
				__result = false;
				return false;
			}

			if(player.ReferenceHub.isDedicatedServer)
			{
				__result = false;
				return false;
			}

			Log.Debug($"UserID: {player.UserId} | PlayerId: {player.Id}", Exiled.Loader.Loader.ShouldDebugBeShown | SanyaPlugin.Instance.Config.IsDebugged);
			Log.Debug($"Permission string: {permission}", Exiled.Loader.Loader.ShouldDebugBeShown | SanyaPlugin.Instance.Config.IsDebugged);

			var plyGroupKey = player.Group != null ? ServerStatic.GetPermissionsHandler()._groups.FirstOrDefault(g => g.Value == player.Group).Key : player.GroupName;
			if(string.IsNullOrEmpty(plyGroupKey))
				plyGroupKey = player.Group != null ? ServerStatic.GetPermissionsHandler()._members.FirstOrDefault(g => g.Key == player.UserId).Value : player.GroupName;

			if(string.IsNullOrEmpty(plyGroupKey))
			{
				__result = false;
				return false;
			}

			Log.Debug($"GroupKey: {plyGroupKey}", Exiled.Loader.Loader.ShouldDebugBeShown | SanyaPlugin.Instance.Config.IsDebugged);

			if(!Permissions.Groups.TryGetValue(plyGroupKey, out var group))
				group = Permissions.DefaultGroup;

			if(group is null)
			{
				__result = false;
				return false;
			}

			const char PERM_SEPARATOR = '.';
			const string ALL_PERMS = ".*";

			if(group.CombinedPermissions.Contains(ALL_PERMS))
			{
				__result = true;
				return false;
			}


			if(permission.Contains(PERM_SEPARATOR))
			{
				var strBuilder = StringBuilderPool.Shared.Rent();
				var seraratedPermissions = permission.Split(PERM_SEPARATOR);

				bool Check(string source) => group.CombinedPermissions.Contains(source, StringComparison.OrdinalIgnoreCase);

				var result = false;
				for(var z = 0; z < seraratedPermissions.Length; z++)
				{
					if(z != 0)
					{
						strBuilder.Length -= ALL_PERMS.Length;

						strBuilder.Append(PERM_SEPARATOR);
					}

					strBuilder.Append(seraratedPermissions[z]);

					if(z == seraratedPermissions.Length - 1)
					{
						result = Check(strBuilder.ToString());
						break;
					}

					strBuilder.Append(ALL_PERMS);
					if(Check(strBuilder.ToString()))
					{
						result = true;
						break;
					}
				}

				StringBuilderPool.Shared.Return(strBuilder);

				__result = result;
				return false;
			}

			__result = group.CombinedPermissions.Contains(permission, StringComparison.OrdinalIgnoreCase);
			return false;
		}
	}

	//override - Fixed
	[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.RemovePlayer))]
	public static class IdleModePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();
			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Brtrue_S) + 1;

			newInst.InsertRange(index, new[]
			{
				new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(RoundSummary), nameof(RoundSummary.singleton))),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RoundSummary), nameof(RoundSummary._roundEnded))),
				new CodeInstruction(OpCodes.Brtrue_S, newInst[newInst.Count - 1].labels[0])
			});

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//override - 10.0.0 checked
	[HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
	public static class IntercomTextPatch
	{
		public static void Prefix(Intercom __instance)
		{
			if(!SanyaPlugin.Instance.Config.IntercomInformation) return;

			int leftdecont = (int)Math.Truncate((DecontaminationController.Singleton.DecontaminationPhases[DecontaminationController.Singleton.DecontaminationPhases.Length - 1].TimeTrigger) - Math.Truncate(DecontaminationController.GetServerTime));
			int leftautowarhead = AlphaWarheadController.Host != null ? (int)Mathf.Clamp(AlphaWarheadController.Host._autoDetonateTime - (AlphaWarheadController.Host._autoDetonateTime - AlphaWarheadController.Host._autoDetonateTimer), 0, AlphaWarheadController.Host._autoDetonateTimer) : -1;
			int nextRespawn = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
			bool isContain = PlayerManager.localPlayer.GetComponent<CharacterClassManager>()._lureSpj.allowContain;
			bool isAlreadyUsed = OneOhSixContainer.used;

			leftdecont = Mathf.Clamp(leftdecont, 0, leftdecont);

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
				$"SCP-106再収容設備：{(isContain ? (isAlreadyUsed ? "使用済み" : "準備完了") : "人員不在")}\n",
				$"軽度収容区画閉鎖まで : {leftdecont / 60:00}:{leftdecont % 60:00}\n",
				$"自動施設爆破開始まで : {leftautowarhead / 60:00}:{leftautowarhead % 60:00}\n",
				$"接近中の部隊突入まで : {nextRespawn / 60:00}:{nextRespawn % 60:00}\n\n"
				);

			if(__instance.Muted)
			{
				contentfix += "アクセスが拒否されました";
			}
			else if(Intercom.AdminSpeaking)
			{
				contentfix += $"施設管理者が放送設備をオーバーライド中";
			}
			else if(__instance.remainingCooldown > 0f)
			{
				contentfix += $"放送設備再起動中 : 残り{Mathf.CeilToInt(__instance.remainingCooldown)}秒";
			}
			else if(__instance.speaker != null)
			{
				contentfix += $"{Player.Get(__instance.speaker).Nickname}が放送中... : 残り{Mathf.CeilToInt(__instance.speechRemainingTime)}秒";
			}
			else
			{
				contentfix += "放送設備準備完了";
			}




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
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp079PlayerScript __instance, ref ushort cameraId, bool lookatRotation)
		{
			if(!SanyaPlugin.Instance.Config.Scp079ExtendEnabled) return true;

			Log.Debug($"[Scp079CameraPatch] {cameraId}:{lookatRotation}", SanyaPlugin.Instance.Config.IsDebugged);

			if(__instance.GetComponent<AnimationController>().curAnim != 1) return true;

			if(SanyaPlugin.Instance.Config.Scp079ExtendLevelFindscp > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelFindscp)
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
				player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Error079NotEnoughTier, 3);
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
					if(SanyaPlugin.Instance.Config.Scp079ExtendLevelNuke > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelNuke)
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
			else if(command.Contains("LOCKDOWN:"))
			{
				if(SanyaPlugin.Instance.Config.Scp079ExtendLevelBomb > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelBomb)
				{
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostBomb > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostBomb, __instance.curMana);
						return false;
					}
					__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostBomb;
					Methods.SpawnGrenade(player.CurrentRoom.Position + new Vector3(0, 2, 0), false, -1, player.ReferenceHub);
					return false;
				}
			}
			else if(command.Contains("DOOR:"))
			{
				if(SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb)
				{
					var door = target.GetComponent<Door>();
					if(door != null && door.DoorName == "SURFACE_GATE")
					{
						if(SanyaPlugin.Instance.Config.Scp079ExtendCostTargetBomb > __instance.curMana)
						{
							__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostTargetBomb, __instance.curMana);
							return false;
						}


						var bombtarget = Player.List.Where(x => x.Position.y > 970 && x.Team != Team.RIP && x.Team != Team.SCP).Random();
						if(bombtarget != null)
						{
							Methods.SpawnGrenade(bombtarget.Position, false, -1, player.ReferenceHub);
							__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostTargetBomb;
						}

						return false;
					}
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
			}
		}
	}

	//transpiler
	[HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.CancelDetonation), new Type[] { typeof(GameObject) })]
	public static class AutoNukeReEnablePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();
			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldarg_0);

			newInst.RemoveRange(index, 3);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
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
			else if(SanyaPlugin.Instance.Config.Scp049StackBody && ragdollInfo.GetDamageType() == DamageTypes.Scp049) return false;
			else return true;
		}
	}

	//not override
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.AddVision))]
	public static class Scp939VisionShieldPatch
	{
		public static void Prefix(Scp939_VisionController __instance, Scp939PlayerScript scp939)
		{
			if(SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount <= 0 || __instance._ccm.CurRole.team == Team.SCP) return;
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
					if(__instance.seeingSCPs[i].scp != null && __instance.seeingSCPs[i].scp.iAm939 && __instance._ccm.CurRole.team != Team.SCP)
					{
						Log.Debug($"[Scp939VisionShieldRemovePatch] {__instance.seeingSCPs[i].scp._hub.nicknameSync.MyNick}({__instance.seeingSCPs[i].scp._hub.characterClassManager.CurClass}) -> {__instance._ccm._hub.nicknameSync.MyNick}({__instance._ccm.CurClass})", SanyaPlugin.Instance.Config.IsDebugged);
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

	//not override
	[HarmonyPatch(typeof(Scp173PlayerScript), nameof(Scp173PlayerScript.FixedUpdate))]
	public static class Scp173ShieldPatch
	{
		private static readonly HashSet<Player> seeingHumans = new HashSet<Player>();

		public static void Postfix(Scp173PlayerScript __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount <= 0 || !__instance.iAm173) return;

			foreach(var ply in Player.List)
			{
				if(!ply.ReferenceHub.characterClassManager.Scp173.SameClass
					&& ply.ReferenceHub.characterClassManager.Scp173.LookFor173(__instance.gameObject, true)
					&& __instance.LookFor173(ply.GameObject, false))
				{
					if(!seeingHumans.Contains(ply))
					{
						Log.Debug($"[Scp173ShieldPatch:Add] {ply.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);
						seeingHumans.Add(ply);
						__instance._ps.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount;
						__instance._ps.unsyncedArtificialHealth = Mathf.Clamp(__instance._ps.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth);
					}
				}
				else if(seeingHumans.Contains(ply))
				{
					Log.Debug($"[Scp173ShieldPatch:Remove] {ply.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);
					seeingHumans.Remove(ply);
					__instance._ps.NetworkmaxArtificialHealth = Mathf.Clamp(__instance._ps.NetworkmaxArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount);
					__instance._ps.unsyncedArtificialHealth = Mathf.Clamp(__instance._ps.unsyncedArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth);
				}
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

	//notify
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.AntiCheatKillPlayer))]
	public static class AntiCheatNotifyPatch
	{
		public static void Prefix(PlayerMovementSync __instance, string message, string code)
		{
			var player = Player.Get(__instance._hub);
			Log.Warn($"[SanyaPlugin] AntiCheatKill Detect:{player.Nickname} [{message}({code})]");
		}
	}

	//Prevent
	[HarmonyPatch(typeof(HintDisplay), nameof(HintDisplay.Show))]
	public static class HintPreventPatch
	{
		public static bool Prefix(HintDisplay __instance, Hint hint)
		{
			if(!SanyaPlugin.Instance.Config.ExHudEnabled) return true;

			if(hint.GetType() == typeof(TranslationHint))
				return false;

			if(hint._effects != null && hint._effects.Length > 0)
				return false;

			return true;
		}
	}

	//transpiler - fix
	[HarmonyPatch(typeof(Scp106PlayerScript), nameof(Scp106PlayerScript.CallCmdUsePortal))]
	public static class Scp106PortalAnimationPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach(var code in instructions)
			{
				if(code.opcode == OpCodes.Call)
					if(code.operand != null
						&& code.operand is MethodBase methodBase
						&& methodBase.Name != nameof(Scp106PlayerScript._DoTeleportAnimation))
						yield return code;
					else
						yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => _DoTeleportAnimation(null)));
				else
					yield return code;
			}
		}

		private static IEnumerator<float> _DoTeleportAnimation(Scp106PlayerScript scp106PlayerScript)
		{
			if(scp106PlayerScript.portalPrefab != null && !scp106PlayerScript.goingViaThePortal)
			{
				scp106PlayerScript.RpcTeleportAnimation();
				scp106PlayerScript.goingViaThePortal = true;
				yield return Timing.WaitForSeconds(3.5f);
				scp106PlayerScript._hub.playerMovementSync.OverridePosition(scp106PlayerScript.portalPrefab.transform.position + Vector3.up * 1.5f, 0f, false);
				yield return Timing.WaitForSeconds(3.5f);
				if(AlphaWarheadController.Host.detonated && scp106PlayerScript.transform.position.y < 800f)
					scp106PlayerScript._hub.playerStats.HurtPlayer(new PlayerStats.HitInfo(9000f, "WORLD", DamageTypes.Nuke, 0), scp106PlayerScript.gameObject, true);
				scp106PlayerScript.goingViaThePortal = false;
			}
		}
	}

	//transpiler
	[HarmonyPatch(typeof(PlayableScps.VisionInformation), nameof(PlayableScps.VisionInformation.GetVisionInformation))]
	public static class Scp096TouchRagePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			if(SanyaPlugin.Instance.Config.Scp096TouchEnrageDistance < 0f)
			{
				foreach(var vanillaInst in instructions.ToList())
					yield return vanillaInst;
				yield break;
			}

			var newInst = instructions.ToList();

			var forceLoSindex = newInst.FindIndex(x => x.opcode == OpCodes.And);
			newInst.RemoveAt(forceLoSindex);
			newInst.RemoveAt(forceLoSindex - 2);

			var fixLoSindex = newInst.FindIndex(x => x.opcode == OpCodes.Ceq) + 2;
			var fixLoSlabel = newInst[fixLoSindex + 2].labels[0];
			newInst.InsertRange(fixLoSindex, new[] {
				new CodeInstruction(OpCodes.Ldloc_2),
				new CodeInstruction(OpCodes.Brfalse_S, fixLoSlabel)
			});

			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Ceq) + 2;
			var label = newInst[index].labels[0];
			var label2 = newInst[index].labels[1];
			newInst[index].labels.RemoveAt(1);


			newInst.InsertRange(index, new[]{
				new CodeInstruction(OpCodes.Ldloca_S, 6),
				new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(UnityEngine.Vector3), nameof(UnityEngine.Vector3.magnitude))),
				new CodeInstruction(OpCodes.Stloc_S, 5),

				new CodeInstruction(OpCodes.Ldloc_S, 7),
				new CodeInstruction(OpCodes.Brfalse_S, label),
				new CodeInstruction(OpCodes.Ldloc_S, 5),
				new CodeInstruction(OpCodes.Ldc_R4, 1.5f),
				new CodeInstruction(OpCodes.Bge_Un_S, label),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Stloc_2),
				new CodeInstruction(OpCodes.Ldc_R4, 0.1f),
				new CodeInstruction(OpCodes.Stloc_3),
			});
			newInst[index].labels.Add(label2);

			var labelindex = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldnull) - 2;
			var labelindex2 = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldarg_S) - 1;
			newInst[labelindex].operand = label2;
			newInst[labelindex2].operand = label2;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//prevent
	[HarmonyPatch(typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.RpcShoot))]
	public static class Scp939PreventRpc
	{
		public static bool Prefix(Scp939PlayerScript __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp939FakeHumansRange < 0) return true;

			Player.Get(__instance.gameObject)?.SendCustomTargetRpc(__instance.netIdentity, typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.RpcShoot), Array.Empty<object>());
			foreach(var target in Player.List.Where(x => x.Team == Team.SCP || x.Team == Team.RIP))
				target.SendCustomTargetRpc(__instance.netIdentity, typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.RpcShoot), Array.Empty<object>());

			foreach(var sanyacomp in UnityEngine.GameObject.FindObjectsOfType<SanyaPluginComponent>())
				if(!sanyacomp.Faked939s.Contains(__instance))
					sanyacomp.player.SendCustomTargetRpc(__instance.netIdentity, typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.RpcShoot), Array.Empty<object>());

			return false;
		}
	}

	//not override
	[HarmonyPatch(typeof(Scp173PlayerScript), nameof(Scp173PlayerScript.Start))]
	public static class Scp173BlinktimePatch
	{
		public static void Postfix(Scp173PlayerScript __instance)
		{
			if(__instance.isLocalPlayer)
			{
				__instance.minBlinkTime = SanyaPlugin.Instance.Config.Scp173MinBlinktime;
				__instance.maxBlinkTime = SanyaPlugin.Instance.Config.Scp173MaxBlinktime;
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(Grenade), nameof(Grenade.OnCollisionEnter))]
	public static class FlashGrenadePatch
	{
		public static void Prefix(Grenade __instance)
		{
			if(!SanyaPlugin.Instance.Config.FlashbangFuseWithCollision) return;
			if(__instance is FlashGrenade)
				__instance.NetworkfuseTime -= __instance.fuseDuration;
		}
	}

	//not override
	[HarmonyPatch(typeof(FlashGrenade), nameof(FlashGrenade.ServersideExplosion))]
	public static class FriendlyFlashRemovePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var friendlyflashindex = newInst.FindIndex(x => 
			x.opcode == OpCodes.Ldfld
			&& x.operand is FieldInfo fieldInfo
			&& fieldInfo?.Name == nameof(FlashGrenade._friendlyFlash)) - 1;

			newInst.RemoveRange(friendlyflashindex, 3);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//override
	[HarmonyPatch(typeof(CustomPlayerEffects.Flashed), nameof(CustomPlayerEffects.Flashed.Flashable))]
	public static class FriendlyFlashAddPatch
	{
		public static bool Prefix(CustomPlayerEffects.Flashed __instance, ref bool __result, ReferenceHub throwerPlayerHub, Vector3 sourcePosition, int ignoreMask)
		{
			__result = ((__instance.Hub != throwerPlayerHub && throwerPlayerHub.weaponManager.GetShootPermission(__instance.Hub.characterClassManager.CurRole.team, false)) || SanyaPlugin.Instance.Handlers.FriendlyFlashEnabled)
				&& !Physics.Linecast(sourcePosition, __instance.Hub.PlayerCameraReference.position, ignoreMask);
			return false;
		}
	}

	//override
	[HarmonyPatch(typeof(LumpOfCoalGrenade), nameof(LumpOfCoalGrenade.OnSpeedCollisionEnter))]
	public static class CoalPatch
	{
		public static bool Prefix(LumpOfCoalGrenade __instance, Collision collision)
		{
			ReferenceHub.TryGetHub(collision.collider.gameObject, out var referenceHub);
			Log.Debug($"{__instance.GetType()} -> {__instance.thrower.hub.nicknameSync.MyNick} => {referenceHub?.nicknameSync.MyNick}");
			if(referenceHub != null && __instance.NetworkthrowerGameObject == referenceHub.gameObject)
				return false;

			if(referenceHub != null && __instance.thrower.hub.weaponManager.GetShootPermission(referenceHub.characterClassManager.CurRole.team))
				__instance.thrower?.hub.playerStats.HurtPlayer(new PlayerStats.HitInfo(232600, __instance.thrower.hub.LoggedNameFromRefHub(), DamageTypes.Grenade, __instance.thrower.hub.queryProcessor.PlayerId), referenceHub.gameObject);

			Timing.RunCoroutine(__instance.DelayKill(collision).CancelWith(__instance.gameObject));
			__instance.GetComponent<MeshCollider>().enabled = false;
			__instance.GetComponent<Rigidbody>().isKinematic = true;
			return false;
		}
	}

	//override
	[HarmonyPatch(typeof(LumpOfSCPCoalGrenade), nameof(LumpOfSCPCoalGrenade.OnSpeedCollisionEnter))]
	public static class CoalScpPatch
	{
		public static bool Prefix(LumpOfSCPCoalGrenade __instance, Collision collision)
		{
			ReferenceHub.TryGetHub(collision.collider.gameObject, out var referenceHub);
			Log.Debug($"{__instance.GetType()} -> {__instance.thrower.hub.nicknameSync.MyNick} => {referenceHub?.nicknameSync.MyNick}");
			if(referenceHub != null && __instance.NetworkthrowerGameObject == referenceHub.gameObject)
				return false;

			if(referenceHub != null)
			{
				List<ReferenceHub> list = new List<ReferenceHub>();
				foreach(KeyValuePair<GameObject, ReferenceHub> keyValuePair in ReferenceHub.GetAllHubs())
				{
					if(keyValuePair.Value.characterClassManager.CurRole.team != Team.RIP && keyValuePair.Value.characterClassManager.CurClass != RoleType.Scp079 && keyValuePair.Value.gameObject != referenceHub.gameObject)
					{
						list.Add(keyValuePair.Value);
					}
				}
				if(list.Count > 0)
				{
					referenceHub.playerMovementSync.OverridePosition(list[UnityEngine.Random.Range(0, list.Count)].playerMovementSync.RealModelPosition, 0f, false);
				}
			}

			Timing.RunCoroutine(__instance.DelayKill(collision).CancelWith(__instance.gameObject));
			__instance.GetComponent<MeshCollider>().enabled = false;
			__instance.GetComponent<Rigidbody>().isKinematic = true;

			return false;
		}
	}
}