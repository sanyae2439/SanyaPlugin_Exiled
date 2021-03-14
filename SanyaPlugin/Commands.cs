using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Extensions;
using Exiled.Permissions.Extensions;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
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
				response = "sanya plugins command. params: <hud/ping/override/actwatch/106/914/nukecap/nukelock/femur/blackout/addscps/ammo/forrcend/now/config>";
				return true;
			}

			switch(arguments.FirstElement().ToLower())
			{
				case "test":
					{
						response = $"test ok.\n";
						// testing zone start

						response += "";

						// testing zone end
						response = response.TrimEnd('\n');

						return true;
					}
				case "doortest":
					{
						if(targetdoor == null)
						{
							var prefab = UnityEngine.Object.FindObjectsOfType<DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("HCZ"));
							var door = UnityEngine.Object.Instantiate(prefab.TargetPrefab, new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))), Quaternion.Euler(Vector3.up * 180f));
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
							Methods.SpawnGrenade(target.Position, false, -1, target.ReferenceHub);
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
							Methods.SpawnGrenade(target.Position, true, -1, target.ReferenceHub);
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
				case "lightint":
					{
						if(arguments.Count < 2)
						{
							response = "need args. <float>";
							return false;
						}

						if(float.TryParse(arguments.At(1), out float arg))
						{
							response = "ok.";
							foreach(var cont in UnityEngine.Object.FindObjectsOfType<FlickerableLightController>())
								cont.ServerSetLightIntensity(arg);
							return true;
						}
						else
						{
							response = "invalid args.";
							return false;
						}
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
						player.ReferenceHub.playerMovementSync.WhitelistPlayer = true;
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

						if(!isActwatchEnabled)
						{
							MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
							{
								writer.WritePackedUInt64(1ul);
								writer.WritePackedUInt32((uint)1);
								writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
								writer.WritePackedUInt32((uint)3);
								writer.WriteByte((byte)1);
							});
							isActwatchEnabled = true;
						}
						else
						{
							MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
							{
								writer.WritePackedUInt64(1ul);
								writer.WritePackedUInt32((uint)1);
								writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
								writer.WritePackedUInt32((uint)3);
								writer.WriteByte((byte)0);
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
				case "914":
					{
						if(arguments.Count > 1)
						{
							if(arguments.At(1).ToLower() == "use")
							{
								if(!Scp914.Scp914Machine.singleton.working)
								{
									Scp914.Scp914Machine.singleton.RpcActivate(NetworkTime.time);
									response = "ok.";
									return true;
								}
								else
								{
									response = "Scp914 now working.";
									return false;
								}

							}
							else if(arguments.At(1).ToLower() == "knob")
							{
								response = $"ok. [{Scp914.Scp914Machine.singleton.knobState}] -> ";
								Scp914.Scp914Machine.singleton.ChangeKnobStatus();
								response += $"[{Scp914.Scp914Machine.singleton.knobState}]";
								return true;
							}
							else
							{
								response = "invalid parameters. (use/knob)";
								return false;
							}
						}
						else
						{
							response = "invalid parameters. (need params)";
							return false;
						}
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
				case "femur":
					{
						ReferenceHub.HostHub.playerInteract.RpcContain106(null);
						response = "ok.";
						return true;
					}
				case "blackout":
					{
						Generator079.mainGenerator.ServerOvercharge(8f, false);
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
				case "ammo":
					{
						if(player == null)
						{
							response = "Only can use with RemoteAdmin.";
							return false;
						}
						player.Ammo[(int)AmmoType.Nato556] = 200;
						player.Ammo[(int)AmmoType.Nato762] = 200;
						player.Ammo[(int)AmmoType.Nato9] = 200;
						response = "ok.";
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
				case "config":
					{
						response = SanyaPlugin.Instance.Config.GetConfigs();
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
