﻿using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using InventorySystem;
using InventorySystem.Items.Pickups;
using MapGeneration;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using RemoteAdmin;
using SanyaPlugin.Functions;
using UnityEngine;

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
		private ItemPickupBase targetitem = null;
		private GameObject targetstation = null;

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
				case "identitytree":
					{
						response = "ok.";
						foreach(var identity in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
						{
							Log.Warn($"{identity.transform.name} (layer{identity.transform.gameObject.layer})");
							Log.Warn($"HasComponents:");
							foreach(var i in identity.transform.gameObject.GetComponents<Component>())
							{
								Log.Warn($"    {i?.name}:{i?.GetType()}");
							}
							Log.Warn($"HasComponentsInChildren:");
							foreach(var i in identity.transform.gameObject.GetComponentsInChildren<Component>())
							{
								Log.Warn($"    {i?.name}:{i?.GetType()}");
							}
							Log.Warn($"HasComponentsInParent:");
							foreach(var i in identity.transform.gameObject.GetComponentsInParent<Component>())
							{
								Log.Warn($"    {i?.name}:{i?.GetType()}");
							}
						}
						return true;
					}
				case "identitypos":
					{
						response = "ok.";
						foreach(var identity in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
						{
							Log.Warn($"{identity.transform.name}{identity.transform.position}");
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
				case "itemtest":
					{
						if(targetitem == null)
						{
							var itemtype = (ItemType)Enum.Parse(typeof(ItemType), arguments.At(1));
							var itemBase = InventoryItemLoader.AvailableItems[itemtype];
							var pickup = UnityEngine.Object.Instantiate(itemBase.PickupDropModel,
								new UnityEngine.Vector3(float.Parse(arguments.At(2)), float.Parse(arguments.At(3)), float.Parse(arguments.At(4))),
								Quaternion.Euler(Vector3.up * float.Parse(arguments.At(5))));
							pickup.Info.ItemId = itemtype;
							pickup.Info.Weight = itemBase.Weight;
							pickup.Info.Locked = true;
							pickup.GetComponent<Rigidbody>().useGravity = false;
							pickup.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(6)), float.Parse(arguments.At(7)), float.Parse(arguments.At(8)));

							targetitem = pickup;
							ItemDistributor.SpawnPickup(pickup);
						}
						else
						{
							NetworkServer.Destroy(targetitem.gameObject);
							targetitem = null;
						}
						response = $"itemtest.";
						return true;
					}
				case "worktest":
					{
						if(targetstation == null)
						{
							var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Station"));
							var station = UnityEngine.Object.Instantiate(prefab, 
								new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))), 
								Quaternion.Euler(Vector3.up * float.Parse(arguments.At(4))));
							station.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(5)), float.Parse(arguments.At(6)), float.Parse(arguments.At(7)));
							targetstation = station;
							NetworkServer.Spawn(station);
						}
						else
						{
							NetworkServer.Destroy(targetstation);
							targetstation = null;
						}
						response = $"worktest.";
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
				case "lightcolor":
					{
						if(arguments.Count == 1)
						{
							response = "Usage: lightcolor r g b or lightcolor reset";
							return false;
						}

						if(arguments.Count == 2 && arguments.At(1) == "reset")
						{
							foreach(var i in FlickerableLightController.Instances)
							{
								i.WarheadLightColor = FlickerableLightController.DefaultWarheadColor;
								i.WarheadLightOverride = false;
							}
							response = "reset ok.";
							return true;
						}

						if(arguments.Count == 4 
							&& float.TryParse(arguments.At(1), out var r) 
							&& float.TryParse(arguments.At(2), out var g) 
							&& float.TryParse(arguments.At(3), out var b))
						{
							foreach(var i in FlickerableLightController.Instances)
							{
								i.WarheadLightColor = new Color(r / 255f, g / 255f, b / 255f);
								i.WarheadLightOverride = true;
							}
							response = $"color set:{r},{g},{b}";
							return true;
						}
						response = $"lightcolor: invalid params.";
						return false;
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
						foreach(var i in Player.List)
							i.SendTextHintNotEffect(arguments.Skip(1).Join(delimiter: " ").Replace("\\n", "\n"), 5);
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
						
						if(SanyaPlugin.Instance.Handlers.Overrided != null)
							SanyaPlugin.Instance.Handlers.Overrided = null;
						else
							SanyaPlugin.Instance.Handlers.Overrided = player;

						response = "ok.";
						return true;
					}
				case "actwatch":
					{
						if(player == null)
						{
							response = "Only can use with RemoteAdmin.";
							return false;
						}

						if(!isActwatchEnabled)
						{
							MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
							{
								writer.WriteUInt64(1ul);
								writer.WriteUInt32((uint)1);
								writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
								writer.WriteUInt32((uint)19);
								writer.WriteByte((byte)1);
							});
							isActwatchEnabled = true;
						}
						else
						{
							MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
							{
								writer.WriteUInt64(1ul);
								writer.WriteUInt32(1);
								writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
								writer.WriteUInt32(19);
								writer.WriteByte(0);
							});
							isActwatchEnabled = false;
						}


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
