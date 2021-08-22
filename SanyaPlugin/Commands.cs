using System;
using System.Diagnostics;
using System.Linq;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
using MEC;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using RemoteAdmin;
using SanyaPlugin.Functions;
using UnityEngine;
using UnityEngine.Profiling;

namespace SanyaPlugin.Commands
{
	[CommandHandler(typeof(GameConsoleCommandHandler))]
	[CommandHandler(typeof(RemoteAdminCommandHandler))]
	class Commands : ICommand
	{
		public string Command { get; } = "sanya";

		public string[] Aliases { get; } = new string[] { "sn" };

		public string Description { get; } = "SanyaPlugin Commands";

		private bool isActwatchEnabled = false;
		private DoorVariant targetdoor = null;

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			Log.Debug($"[Commands] Sender:{sender.LogName} args:{arguments.Count}", SanyaPlugin.Instance.Config.IsDebugged);

			Player player = null;
			if(sender is PlayerCommandSender playerCommandSender) player = Player.Get(playerCommandSender.SenderId);

			if(player != null && !player.CheckPermission("sanya.command"))
			{
				response = "Permission denied.";
				return false;
			}

			if(arguments.Count == 0)
			{
				response = "sanya plugins command. params: <hud/ping/override/actwatch/airbomb/106/914/nukecap/nukelock/lure/femur/blackout/addscps/ammo/forrcend/now/configs>";
				return true;
			}

			switch(arguments.FirstElement().ToLower())
			{
				case "test":
					{
						response = $"test ok.\n";
						// testing zone start



						// testing zone end
						response = response.TrimEnd('\n');

						return true;
					}
				case "frametime":
					{
						response = $"frametime:{Time.deltaTime}";
						return true;
					}
				case "coroutines":
					{
						response = $"FixedUpdate:{Timing.Instance.FixedUpdateCoroutines} Update:{Timing.Instance.UpdateCoroutines}";
						return true;
					}
				case "airbomb":
					{
						response = "ok.";
						if(!Coroutines.isAirBombGoing)
							SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb(), Segment.FixedUpdate));
						else
							Coroutines.isAirBombGoing = false;
						return true;
					}
				case "checkobj":
					{
						response = "ok.";
						if(Physics.Raycast(player.Position + player.CameraTransform.forward, player.CameraTransform.forward, out var casy))
						{
							Log.Warn($"{casy.transform.name} (layer{casy.transform.gameObject.layer})");
							Log.Warn($"HasComponents:");
							foreach(var i in casy.transform.gameObject.GetComponents<Component>())
							{
								Log.Warn($"    {i.name}:{i.GetType()}");
							}
							Log.Warn($"HasComponentsInChildren:");
							foreach(var i in casy.transform.gameObject.GetComponentsInChildren<Component>())
							{
								Log.Warn($"    {i.name}:{i.GetType()}");
							}
							Log.Warn($"HasComponentsInParent:");
							foreach(var i in casy.transform.gameObject.GetComponentsInParent<Component>())
							{
								Log.Warn($"    {i.name}:{i.GetType()}");
							}
						}
						return true;
					}
				case "avlcol":
					{
						response = "Available colors:\n";
						foreach(var i in ReferenceHub.HostHub.serverRoles.NamedColors.OrderBy(x => x.Restricted))
							response += $"[#{i.ColorHex}] {i.Name,-13} {(i.Restricted ? "Restricted" : "Not Restricted")}\n";
						return true;
					}
				case "doortest":
					{
						if(targetdoor == null)
						{
							var prefab = UnityEngine.Object.FindObjectsOfType<DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("HCZ"));
							var door = UnityEngine.Object.Instantiate(prefab.TargetPrefab, new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))), Quaternion.Euler(Vector3.up * 180f));
							door.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(4)), float.Parse(arguments.At(5)), float.Parse(arguments.At(6)));
							targetdoor = door;
							NetworkServer.Spawn(door.gameObject);
						}
						else
						{
							NetworkServer.Destroy(targetdoor.gameObject);
							targetdoor = null;
						}
						response = $"doortest.";
						return true;
					}
				case "sinkhole":
					{
						if(SanyaPlugin.Instance.Handlers.Sinkhole == null)
						{
							response = "Sinkhole is null.";
							return false;
						}
						else
						{
							Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkhole, player.Position);
							response = "ok.";
							return true;
						}
					}
				case "frag":
					{
						var target = Player.Get(int.Parse(arguments.At(1)));

						if(target != null)
						{
							Methods.SpawnGrenade(target.Position, Data.GRENADE_ID.FRAG_NADE, -1, target.ReferenceHub);
							response = $"{target.Nickname} ok.";
							return true;
						}
						else
						{
							response = $"target is null.";
							return false;
						}
					}
				case "flash":
					{
						var target = Player.Get(int.Parse(arguments.At(1)));

						if(target != null)
						{
							Methods.SpawnGrenade(target.Position, Data.GRENADE_ID.FLASH_NADE, -1, target.ReferenceHub);
							response = $"{target.Nickname} ok.";
							return true;
						}
						else
						{
							response = $"target is null.";
							return false;
						}
					}
				case "ball":
					{
						var target = Player.Get(int.Parse(arguments.At(1)));

						if(target != null)
						{
							Methods.SpawnGrenade(target.Position + Vector3.up, Data.GRENADE_ID.SCP018_NADE, -1, target.ReferenceHub);
							response = $"{target.Nickname} ok.";
							return true;
						}
						else
						{
							response = $"target is null.";
							return false;
						}
					}
				case "scale":
					{
						var target = Player.Get(int.Parse(arguments.At(1)));

						target.Scale = new UnityEngine.Vector3(
							float.Parse(arguments.At(2)),
							float.Parse(arguments.At(3)),
							float.Parse(arguments.At(4))
						);

						response = $"{target.Nickname} ok.";
						return true;
					}
				case "args":
					{
						response = "ok.\n";
						for(int i = 0; i < arguments.Count; i++)
						{
							response += $"[{i}]{arguments.At(i)}\n";
						}
						response = response.TrimEnd('\n');
						return true;
					}
				case "htest":
					{
						player.SendTextHintNotEffect(arguments.Skip(1).Join(delimiter: " ").Replace("\\n", "\n"), 5);
						response = "ok.";
						return true;
					}
				case "hud":
					{
						var comp = player.GameObject.GetComponent<SanyaPluginComponent>();
						response = $"ok.{comp.DisableHud} -> ";
						comp.DisableHud = !comp.DisableHud;
						response += $"{comp.DisableHud}";
						return true;
					}
				case "hudall":
					{
						foreach(var i in Player.List)
							if(i.ReferenceHub.TryGetComponent<SanyaPluginComponent>(out var sanya))
								sanya.DisableHud = bool.Parse(arguments.At(1));
						response = $"set to {bool.Parse(arguments.At(1))}";
						return true;
					}
				case "ping":
					{
						response = "Pings:\n";

						foreach(var ply in Player.List)
						{
							response += $"{ply.Nickname} : {LiteNetLib4MirrorServer.Peers[ply.Connection.connectionId].Ping}ms\n";
						}
						return true;
					}
				case "acwh":
					{
						response = "ok.";
						player.ReferenceHub.playerMovementSync.NoclipWhitelisted = true;
						return true;
					}
				case "override":
					{
						response = "ok.";
						SanyaPlugin.Instance.Handlers.Overrided = player;
						return true;
					}
				case "actwatch":
					{
						if(player == null)
						{
							response = "Only can use with RemoteAdmin.";
							return false;
						}

						//if(!isActwatchEnabled)
						//{
						//	MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
						//	{
						//		writer.WritePackedUInt64(1ul);
						//		writer.WritePackedUInt32((uint)1);
						//		writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
						//		writer.WritePackedUInt32((uint)3);
						//		writer.WriteByte((byte)1);
						//	});
						//	isActwatchEnabled = true;
						//}
						//else
						//{
						//	MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
						//	{
						//		writer.WritePackedUInt64(1ul);
						//		writer.WritePackedUInt32((uint)1);
						//		writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
						//		writer.WritePackedUInt32((uint)3);
						//		writer.WriteByte((byte)0);
						//	});
						//	isActwatchEnabled = false;
						//}


						response = $"ok. [{isActwatchEnabled}]";
						return true;
					}
				case "106":
					{
						foreach(var pocketteleport in UnityEngine.Object.FindObjectsOfType<PocketDimensionTeleport>())
						{
							pocketteleport.SetType(PocketDimensionTeleport.PDTeleportType.Exit);
						}
						response = "ok.";
						return true;
					}
				case "nukecap":
					{
						var outsite = UnityEngine.Object.FindObjectOfType<AlphaWarheadOutsitePanel>();
						response = $"ok.[{outsite.keycardEntered}] -> ";
						outsite.NetworkkeycardEntered = !outsite.keycardEntered;
						response += $"[{outsite.keycardEntered}]";
						return true;
					}
				case "nukelock":
					{
						response = $"ok.[{AlphaWarheadController.Host._isLocked}] -> ";
						AlphaWarheadController.Host._isLocked = !AlphaWarheadController.Host._isLocked;
						response += $"[{AlphaWarheadController.Host._isLocked}]";
						return true;
					}
				case "lure":
					{
						var lure = UnityEngine.Object.FindObjectOfType<LureSubjectContainer>();
						response = $"ok.[{lure.allowContain}] -> ";
						lure.NetworkallowContain = !lure.allowContain;
						response += $"[{lure.allowContain}]";
						return true;
					}
				case "femur":
					{
						ReferenceHub.HostHub.playerInteract.RpcContain106(null);
						response = "ok.";
						return true;
					}
				case "addscps":
					{
						response = $"ok.{RoundSummary.singleton.classlistStart.scps_except_zombies} -> ";
						RoundSummary.singleton.classlistStart.scps_except_zombies++;
						response += $"[{RoundSummary.singleton.classlistStart.scps_except_zombies}]";
						return true;
					}
				case "forceend":
					{
						RoundSummary.singleton.ForceEnd();
						response = "Force Ended!";
						return true;
					}
				case "now":
					{
						response = $"now ticks:{TimeBehaviour.CurrentTimestamp()}";
						return true;
					}
				case "resetflag":
					{
						response = "ok.";
						PlayerDataManager.ReloadParams();
						return true;
					}
				case "configs":
					{
						response = Methods.ToStringPropertiesAndFields(SanyaPlugin.Instance.Config);
						return true;
					}
				default:
					{
						response = "invalid params.";
						return false;
					}
			}
		}
	}
}
