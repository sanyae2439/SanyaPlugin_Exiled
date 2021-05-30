using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Assets._Scripts.Dissonance;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Grenades;
using HarmonyLib;
using Hints;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LightContainmentZoneDecontamination;
using MapGeneration;
using Mirror;
using NorthwoodLib.Pools;
using Respawning;
using Respawning.NamingRules;
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
			if(PlayerManager.localPlayer == null || SeedSynchronizer.Seed == 0) return;
			Log.Debug($"[NTFUnitPatch] unit:{regular}", SanyaPlugin.Instance.Config.IsDebugged);

			if(SanyaPlugin.Instance.Config.CassieSubtitle && !SanyaPlugin.Instance.Config.DisableEntranceAnnounce)
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
			if(SanyaPlugin.Instance.Config.DisableEntranceAnnounce && type == RespawnEffectsController.EffectType.UponRespawn) return false;
			else return true;
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
						IDamageableDoor damageableDoor;
						if((damageableDoor = (collider.GetComponentInParent<DoorVariant>() as IDamageableDoor)) != null)
						{
							damageableDoor.ServerDamage(100f, DoorDamageType.Grenade);
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
				if(SanyaPlugin.Instance.Config.Scp079ManaCost.TryGetValue(ability.label, out var value))
					ability.mana = value;
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
				&& target.TryGetComponent<DoorVariant>(out var targetdoor)
				&& targetdoor is Interactables.Interobjects.PryableDoor)
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
						}
						else
							AlphaWarheadController.Host.CancelDetonation();

						__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostNuke;
						__instance.AddInteractionToHistory(GameObject.Find(__instance.currentZone + "/" + __instance.currentRoom + "/Scp079Speaker"), args[0], true);
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

					GameObject gameObject3 = null;
					List<Scp079Interactable> list2 = new List<Scp079Interactable>();
					foreach(Scp079Interactable scp079Interactable4 in Interface079.singleton.allInteractables)
						if(scp079Interactable4 != null)
							foreach(Scp079Interactable.ZoneAndRoom zoneAndRoom in scp079Interactable4.currentZonesAndRooms)
								if(zoneAndRoom.currentRoom == __instance.currentRoom 
									&& zoneAndRoom.currentZone == __instance.currentZone 
									&& scp079Interactable4.transform.position.y - 100f < __instance.currentCamera.transform.position.y 
									&& !list2.Contains(scp079Interactable4))
									list2.Add(scp079Interactable4);

					foreach(Scp079Interactable scp079Interactable5 in list2)
						if(scp079Interactable5.type != Scp079Interactable.InteractableType.Door)
							if(scp079Interactable5.type == Scp079Interactable.InteractableType.Lockdown)
								gameObject3 = scp079Interactable5.gameObject;

					__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostBomb;
					__instance.AddInteractionToHistory(gameObject3, args[0], true);

					var players = player.CurrentRoom?.Players.Where(x => x.Team != Team.SCP);
					if(players?.Count() > 0)
						Methods.SpawnGrenade(players.Random().Position + new Vector3(0, 2, 0), false, -1, player.ReferenceHub);
					else
						Methods.SpawnGrenade(player.CurrentRoom.Position + new Vector3(0, 2, 0), false, -1, player.ReferenceHub);	
					return false;
				}
			}
			else if(command.Contains("ELEVATORUSE:"))
			{
				if(SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb)
				{
					if(__instance.currentZone == "Outside")
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
							__instance.AddInteractionToHistory(target, args[0], true);
							return false;
						}
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
			if(__instance is FlashGrenade flashGrenade && flashGrenade.DisableGameObject)
			{
				__instance.NetworkfuseTime = NetworkTime.time + 1.0;
				flashGrenade.DisableGameObject = false;
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(FragGrenade), nameof(FragGrenade.ServersideExplosion))]
	public static class FragPryGatePatch
	{
		public static void Prefix(FragGrenade __instance)
		{
			foreach(Collider collider in Physics.OverlapSphere(__instance.transform.position, __instance.chainTriggerRadius, __instance.damageLayerMask))
			{
				PryableDoor componentInParent = collider.GetComponentInParent<PryableDoor>();
				if(componentInParent != null && !componentInParent.NetworkTargetState)
					componentInParent.TryPryGate();
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(FlashGrenade), nameof(FlashGrenade.ServersideExplosion))]
	public static class FriendlyFlashRemovePatch
	{
		public static bool Prefix(FlashGrenade __instance, ref bool __result)
		{
			foreach(GameObject gameObject in PlayerManager.players)
			{
				Vector3 position = __instance.transform.position;
				Player thrower = Player.Get(__instance.thrower.gameObject);
				Player target = Player.Get(gameObject);
				ReferenceHub hub = ReferenceHub.GetHub(gameObject);
				Flashed effect = hub.playerEffectsController.GetEffect<Flashed>();
				if(effect != null && !(__instance.thrower == null) && effect.Flashable(ReferenceHub.GetHub(__instance.thrower.gameObject), position, __instance._ignoredLayers))
				{
					float num = __instance.powerOverDistance.Evaluate(Vector3.Distance(gameObject.transform.position, position) / ((position.y > 900f)
						? __instance.distanceMultiplierSurface
						: __instance.distanceMultiplierFacility)) * __instance.powerOverDot.Evaluate(Vector3.Dot(hub.PlayerCameraReference.forward, (hub.PlayerCameraReference.position - position).normalized));
					byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(num * 10f * __instance.maximumDuration), 1, 255);
					if(b >= effect.Intensity && num > 0f)
					{
						if(target != thrower && !thrower.IsEnemy(target.Team) && target.GameObject.TryGetComponent<SanyaPluginComponent>(out var comp))
						{
							comp.AddHudBottomText($"<color=#ffff00><size=25>味方の{thrower.Nickname}よりダメージを受けました[FlashGrenade]</size></color>", 5);
							thrower.ReferenceHub.GetComponent<SanyaPluginComponent>()?.AddHudBottomText($"味方の<color=#ff0000><size=25>{comp.player.Nickname}へダメージを与えました[FlashGrenade]</size></color>", 5);
						}

						if(hub.characterClassManager.IsAnyScp())
							hub.playerEffectsController.ChangeEffectIntensity<Flashed>(1);

						num *= 2f;
						hub.playerEffectsController.EnableEffect<Amnesia>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Deafened>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Blinded>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Concussed>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Panic>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Exhausted>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Disabled>(num * __instance.maximumDuration, true);
					}
				}
			}
			__result = ((Func<bool>)Activator.CreateInstance(typeof(Func<bool>), __instance, typeof(EffectGrenade).GetMethod(nameof(EffectGrenade.ServersideExplosion)).MethodHandle.GetFunctionPointer()))();
			return false;
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

	//transpiler
	[HarmonyPatch(typeof(Handcuffs), nameof(Handcuffs.CallCmdCuffTarget))]
	public static class RemoveHandcuffsItemPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldc_I4_M1) - 4;

			newInst.RemoveRange(index, 6);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//transpiler
	[HarmonyPatch(typeof(CharacterClassManager), nameof(CharacterClassManager.CallCmdRegisterEscape))]
	public static class RemoveEscapeCounterPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var cuffedClassDindex = newInst.FindIndex(x => x.opcode == OpCodes.Ldsfld
				&& x.operand is FieldInfo fieldInfo
				&& fieldInfo.Name == nameof(RoundSummary.escaped_scientists)
			);
			newInst.RemoveRange(cuffedClassDindex, 4);

			var cuffedScientistindex = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldsfld
				&& x.operand is FieldInfo fieldInfo
				&& fieldInfo.Name == nameof(RoundSummary.escaped_ds)
			);
			newInst.RemoveRange(cuffedScientistindex, 4);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//not override
	[HarmonyPatch(typeof(Lift), nameof(Lift.MovePlayers))]
	public static class MoveSinkholePatch
	{
		public static void Postfix(Lift __instance, Transform target)
		{
			if(SanyaPlugin.Instance.Handlers.Sinkhole != null
				&& __instance.InRange(SanyaPlugin.Instance.Handlers.Sinkhole.transform.position, out var gameObject, 1f, 2f, 1f)
				&& gameObject.transform != target)
			{
				Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkhole, target.TransformPoint(gameObject.transform.InverseTransformPoint(SanyaPlugin.Instance.Handlers.Sinkhole.transform.position)));
			}
		}
	}

	//not override
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.CanSee))]
	public static class Scp939OverAllPatch
	{
		public static void Postfix(Scp939_VisionController __instance, Scp939PlayerScript scp939, ref bool __result)
		{
			if(SanyaPlugin.Instance.Handlers.Overrided?.ReferenceHub.nicknameSync.Network_myNickSync == scp939._hub.nicknameSync.Network_myNickSync)
				__result = true;
		}
	}

	//transpiler
	[HarmonyPatch(typeof(Scp207), nameof(Scp207.PublicUpdate))]
	public static class Scp207PreventDamageForScpPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ret);
			var brindex = newInst.FindIndex(x => x.opcode == OpCodes.Brtrue_S);
			var exitlabel = newInst[index + 1].labels[0];
			var retlabel = newInst[index].labels[0];

			newInst[brindex].opcode = OpCodes.Brfalse_S;
			newInst[brindex].operand = retlabel;

			newInst.InsertRange(index, new[]{
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerEffect),nameof(PlayerEffect.Hub))),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ReferenceHub),nameof(ReferenceHub.characterClassManager))),
				new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(CharacterClassManager),nameof(CharacterClassManager.IsAnyScp))),
				new CodeInstruction(OpCodes.Brfalse_S, exitlabel)
			});

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//not override
	[HarmonyPatch(typeof(LockerManager), nameof(LockerManager.Generate))]
	public static class LockerAddItemPatch
	{
		public static void Prefix(LockerManager __instance)
		{
			if(!SanyaPlugin.Instance.Config.SpawnAddItems) return;

			Log.Debug($"Item adding...", SanyaPlugin.Instance.Config.IsDebugged);
			var list = new List<SpawnableItem>(__instance.items);

			var glocker556_1 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_1.inventoryId = ItemType.Radio;

			var glocker556_2 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_2.inventoryId = ItemType.KeycardNTFLieutenant;

			var glocker556_3 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_3.inventoryId = ItemType.Adrenaline;

			var glocker556_4 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_4.inventoryId = ItemType.GrenadeFrag;

			var glocker_b_small_1 = list.First(x => x.itemTag == "glocker-b-small" && x.inventoryId == ItemType.Ammo556);
			glocker_b_small_1.inventoryId = ItemType.Radio;
			glocker_b_small_1.chanceOfSpawn = 100;
			glocker_b_small_1.copies = 0;

			var misclocker_1 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo762);
			misclocker_1.inventoryId = ItemType.GunCOM15;
			misclocker_1.copies = 0;

			var misclocker_2 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo556);
			misclocker_2.inventoryId = ItemType.Coin;
			misclocker_2.copies = 3;

			var misclocker_3 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo9mm);
			misclocker_3.inventoryId = ItemType.Radio;
			misclocker_3.copies = 0;

			var misclocker_4 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardScientist);
			misclocker_4.inventoryId = ItemType.KeycardGuard;
			misclocker_4.copies = 0;

			var misclocker_5 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardScientist);
			misclocker_5.inventoryId = ItemType.KeycardSeniorGuard;
			misclocker_5.copies = 0;
			misclocker_5.chanceOfSpawn = 50;

			var misclocker_6 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardZoneManager);
			misclocker_6.inventoryId = ItemType.KeycardScientistMajor;
			misclocker_6.copies = 0;

			var misclocker_7 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Painkillers);
			misclocker_7.inventoryId = ItemType.SCP500;
			misclocker_7.copies = 0;
			misclocker_7.chanceOfSpawn = 50;

			var misclocker_8 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Flashlight);
			misclocker_8.inventoryId = ItemType.GrenadeFlash;
			misclocker_8.copies = 0;

			var misclocker_9 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Flashlight);
			misclocker_9.inventoryId = ItemType.SCP207;
			misclocker_9.copies = 0;
			misclocker_9.chanceOfSpawn = 50;

			__instance.items = list.ToArray();
			Log.Debug($"Item add completed.", SanyaPlugin.Instance.Config.IsDebugged);
		}
	}

	//not override
	[HarmonyPatch(typeof(HostItemSpawner), nameof(HostItemSpawner.Spawn))]
	public static class ItemSpawnerAddPatch
	{
		public static void Prefix()
		{
			if(!SanyaPlugin.Instance.Config.SpawnAddItems) return;
			Log.Debug($"SpawnerItem adding...", SanyaPlugin.Instance.Config.IsDebugged);
			var list = RandomItemSpawner.singleton.pickups;

			var lcarmory_ammo_762 = list.First(x => x.posID == "LC_Armory_Ammo" && x.itemID == ItemType.Ammo762);
			lcarmory_ammo_762.itemID = ItemType.Radio;

			var lcarmory_ammo_9mm = list.First(x => x.posID == "LC_Armory_Ammo" && x.itemID == ItemType.Ammo9mm);
			lcarmory_ammo_9mm.itemID = ItemType.Disarmer;

			var lcarmory_com15 = list.First(x => x.posID == "LC_Armory_Pistol" && x.itemID == ItemType.GunCOM15);
			lcarmory_com15.itemID = ItemType.Medkit;

			var lcarmory_mp7 = list.First(x => x.posID == "LC_Armory" && x.itemID == ItemType.GunMP7);
			lcarmory_mp7.itemID = ItemType.GunLogicer;

			var room012_keycard = list.First(x => x.posID == "012_mScientist_keycard" && x.itemID == ItemType.KeycardZoneManager);
			room012_keycard.itemID = ItemType.KeycardScientistMajor;

			var cafe = list.First(x => x.posID == "Cafe_Scientist_keycard" && x.itemID == ItemType.KeycardScientist);
			cafe.itemID = ItemType.KeycardScientistMajor;

			var servers = list.First(x => x.posID == "Servers" && x.itemID == ItemType.KeycardScientist);
			servers.itemID = ItemType.KeycardSeniorGuard;

			var nuke = list.First(x => x.posID == "Nuke" && x.itemID == ItemType.KeycardGuard);
			nuke.itemID = (ItemType)UnityEngine.Random.Range((int)ItemType.KeycardJanitor, (int)ItemType.KeycardO5+1);

			RandomItemSpawner.singleton.pickups = list.ToArray();
			Log.Debug($"SpawnerItem added.", SanyaPlugin.Instance.Config.IsDebugged);
		}
	}

	//transpiler
	[HarmonyPatch(typeof(Decontaminating), nameof(Decontaminating.PublicUpdate))]
	public static class RemoveDecontPosCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fieldInfo && fieldInfo.Name == nameof(PlayerEffect.Hub)) - 1;

			newInst.RemoveRange(index, 15);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//override
	[HarmonyPatch(typeof(Lift), nameof(Lift.CheckMeltPlayer))]
	public static class CheckDecontLiftPatch
	{
		public static bool Prefix(Lift __instance, GameObject ply)
		{
			if(!ReferenceHub.TryGetHub(ply, out var referenceHub) 
				|| referenceHub.playerMovementSync.RealModelPosition.y >= 200f 
				|| referenceHub.playerMovementSync.RealModelPosition.y <= -200f)
				return false;
			referenceHub.playerEffectsController.EnableEffect<Decontaminating>(0f, false);
			return false;
		}
	}

	//not override
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.ForceLastSafePosition))]
	public static class AntiCheatCheckPatch
	{
		public static bool Prefix(PlayerMovementSync __instance)
		{
			if(__instance._hub.characterClassManager.CurClass == RoleType.Scp173 
				&& Scp173PlayerScript._blinkTimeRemaining > 0f)
			{
				__instance.RealModelPosition = __instance._receivedPosition;
				__instance._lastSafePosition = __instance._receivedPosition;
				__instance._lastSafePosition2 = __instance._receivedPosition;
				__instance._lastSafePosition3 = __instance._receivedPosition;
				__instance._lastSafePositionDistance = 0f;
				return false;
			}
			return true;
		}
	}

	//transpiler
	[HarmonyPatch(typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.CallCmdShoot))]
	public static class Scp939PreventCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldc_R4) + 2;
			var nextlabel = newInst[newInst.FindIndex(x => x.opcode == OpCodes.Ret) - 1].operand;
			
			newInst[index - 1].opcode = OpCodes.Ble_Un_S;
			newInst[index - 1].operand = nextlabel;
			newInst.RemoveRange(index, 12);

			var popindex = newInst.FindIndex(x => x.opcode == OpCodes.Pop) + 1;
			newInst.RemoveRange(popindex, 17);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//transpiler
	[HarmonyPatch(typeof(Scp049_2PlayerScript), nameof(Scp049_2PlayerScript.CallCmdHurtPlayer))]
	public static class Scp0492PreventCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fieldInfo && fieldInfo.Name == nameof(Scp049_2PlayerScript._hub)) - 1;

			newInst.RemoveRange(index, 13);

			var label = generator.DefineLabel();
			newInst[index].labels.Add(label);
			newInst[index - 2].operand = label;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//fix
	[HarmonyPatch(typeof(MEC.Timing), nameof(MEC.Timing.RunCoroutine), new Type[] { typeof(IEnumerator<float>) })]
	public static class FixDefaultSegmentPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			newInst.Find(x => x.opcode == OpCodes.Ldc_I4_0).opcode = OpCodes.Ldc_I4_1;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}

	//override
	[HarmonyPatch(typeof(MEC.Timing), nameof(MEC.Timing.RunCoroutine), new Type[] { typeof(IEnumerator<float>), typeof(MEC.Segment) })]
	public static class FixOverrideFixedUpdateCoroutinePatch
	{
		public static void Prefix(ref MEC.Segment segment) => segment = MEC.Segment.FixedUpdate;
	}

	//override - exiled
	[HarmonyPatch(typeof(Scp106PlayerScript), nameof(Scp106PlayerScript.CallCmdMovePlayer))]
	public static class Scp106AttackPatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp106PlayerScript __instance, GameObject ply, int t)
		{
			Log.Debug($"SanyaPlugins Overrided:Scp106PlayerScript", SanyaPlugin.Instance.Config.IsDebugged);
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
				Exiled.API.Features.Log.Error($"{typeof(Scp106AttackPatch).FullName}:\n{e}");

				return true;
			}
		}
	}
}