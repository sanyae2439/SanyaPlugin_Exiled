using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin.Patches
{
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
						if(SanyaPlugin.Instance.Config.Scp079ExtendCostNuke > __instance.curMana && !player.IsBypassModeEnabled)
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
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostBomb > __instance.curMana && !player.IsBypassModeEnabled)
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
						Methods.SpawnGrenade(players.Random().Position + new Vector3(0, 2, 0), GRENADE_ID.FRAG_NADE, -1, player.ReferenceHub);
					else
						Methods.SpawnGrenade(player.CurrentRoom.Position + new Vector3(0, 2, 0), GRENADE_ID.FRAG_NADE, -1, player.ReferenceHub);
					return false;
				}
			}
			else if(command.Contains("ELEVATORUSE:"))
			{
				if(SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelTargetBomb)
				{
					if(__instance.currentZone == "Outside")
					{
						if(SanyaPlugin.Instance.Config.Scp079ExtendCostTargetBomb > __instance.curMana && !player.IsBypassModeEnabled)
						{
							__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostTargetBomb, __instance.curMana);
							return false;
						}


						var bombtarget = Player.List.Where(x => x.Position.y > 970 && x.Team != Team.RIP && x.Team != Team.SCP).Random();
						if(bombtarget != null)
						{
							Methods.SpawnGrenade(bombtarget.Position, GRENADE_ID.FRAG_NADE, -1, player.ReferenceHub);
							Methods.SpawnGrenade(bombtarget.Position, GRENADE_ID.FRAG_NADE, -1, player.ReferenceHub);
							Methods.SpawnGrenade(bombtarget.Position, GRENADE_ID.FRAG_NADE, -1, player.ReferenceHub);
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
}
