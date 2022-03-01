using System;
using CommandSystem;
using SanyaPlugin.Commands.Debugs;
using SanyaPlugin.Commands.Items;
using SanyaPlugin.Commands.Utils;

namespace SanyaPlugin.Commands
{
	[CommandHandler(typeof(GameConsoleCommandHandler))]
	[CommandHandler(typeof(RemoteAdminCommandHandler))]
	[CommandHandler(typeof(ClientCommandHandler))]
	public class SanyaCommand : ParentCommand
	{
		public SanyaCommand() => LoadGeneratedCommands();

		public override string Command { get; } = "sanya";

		public override string[] Aliases { get; }

		public override string Description { get; } = "SanyaPlugin Commands";

		public override void LoadGeneratedCommands()
		{
			//Debugs
			RegisterCommand(new TestCommand());
			RegisterCommand(new CoroutinesCommand());
			RegisterCommand(new CheckObjectCommand());
			RegisterCommand(new CheckComponentsCommand());

			//Items
			RegisterCommand(new TantrumCommand());

			//Utils
			RegisterCommand(new ActWatchCommand());
			RegisterCommand(new OverrideCommand());
			RegisterCommand(new Scp914Command());
			RegisterCommand(new TpPosCommand());
		}

		protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			response = "正しいコマンドを入力してださい。";
			return false;
		}

		//private bool isActwatchEnabled = false;
		//private DoorVariant targetdoor = null;
		//private ItemPickupBase targetitem = null;
		//private GameObject targetstation = null;
		//private GameObject targetTarget = null;
		//private PrimitiveObjectToy targetPrimitive = null;
		//private LightSourceToy targetLight = null;

		//public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		//{
		//	Log.Debug($"[Commands] Sender:{sender.LogName} args:{arguments.Count}", SanyaPlugin.Instance.Config.IsDebugged);

		//	Player player = null;
		//	if(sender is PlayerCommandSender playerCommandSender) player = Player.Get(playerCommandSender.SenderId);

		//	if(player != null && !player.CheckPermission("sanya.command"))
		//	{
		//		response = "Permission denied.";
		//		return false;
		//	}

		//	if(arguments.Count == 0)
		//	{
		//		response = "sanya plugins command. params: <hud/ping/override/actwatch/106/914/nukecap/nukelock/lure/femur/blackout/addscps/ammo/forrcend/now/configs>";
		//		return true;
		//	}

		//	switch(arguments.FirstElement().ToLower())
		//	{
		//		case "lighttest":
		//			{
		//				if(targetLight == null)
		//				{
		//					var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("LightSource"));
		//					var pobject = UnityEngine.Object.Instantiate(prefab.GetComponent<LightSourceToy>());
		//					targetLight = pobject;

		//					NetworkServer.Spawn(pobject.gameObject, ownerConnection: null);
		//				}

		//				targetLight.transform.position = new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3)));
		//				targetLight.NetworkLightIntensity = float.Parse(arguments.At(4));
		//				targetLight.NetworkLightRange = float.Parse(arguments.At(5));
		//				targetLight.NetworkLightShadows = bool.Parse(arguments.At(6));
		//				response = $"lighttest.";
		//				return true;
		//			}
		//		case "offtest":
		//			{
		//				if(targetPrimitive == null)
		//				{
		//					var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Primitive"));
		//					var pobject = UnityEngine.Object.Instantiate(prefab.GetComponent<PrimitiveObjectToy>());

		//					pobject.NetworkScale = Vector3.one;
		//					pobject.NetworkMaterialColor = Color.black;
		//					targetPrimitive = pobject;

		//					NetworkServer.Spawn(pobject.gameObject, ownerConnection: null);
		//				}

		//				targetPrimitive.NetworkPrimitiveType = PrimitiveType.Cube;
		//				targetPrimitive.transform.SetParentAndOffset(player.CurrentRoom.transform, new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))));
		//				targetPrimitive.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(4)), float.Parse(arguments.At(5)), float.Parse(arguments.At(6)));
		//				response = "offtest.";
		//				return true;
		//			}
		//		case "walltest":
		//			{
		//				if(targetPrimitive == null)
		//				{
		//					var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Primitive"));
		//					var pobject = UnityEngine.Object.Instantiate(prefab.GetComponent<PrimitiveObjectToy>());

		//					pobject.NetworkScale = Vector3.one;
		//					pobject.NetworkMaterialColor = Color.black;
		//					targetPrimitive = pobject;

		//					NetworkServer.Spawn(pobject.gameObject, ownerConnection: null);
		//				}

		//				targetPrimitive.NetworkPrimitiveType = PrimitiveType.Cube;
		//				targetPrimitive.transform.position = new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3)));
		//				targetPrimitive.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(4)), float.Parse(arguments.At(5)), float.Parse(arguments.At(6)));
		//				response = $"walltest.";
		//				return true;
		//			}
		//		case "targettest":
		//			{
		//				if(targetTarget == null)
		//				{
		//					var gameObject = UnityEngine.Object.Instantiate(CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("dboyTarget")), 
		//						new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))),
		//						Quaternion.Euler(Vector3.up * float.Parse(arguments.At(4))));
		//					targetTarget = gameObject;
		//					NetworkServer.Spawn(gameObject);
		//				}
		//				else
		//				{
		//					NetworkServer.Destroy(targetTarget);
		//					targetTarget = null;
		//				}
		//				response = $"targettest.";
		//				return true;
		//			}
		//		case "itemtest":
		//			{
		//				if(targetitem == null)
		//				{
		//					var itemtype = (ItemType)Enum.Parse(typeof(ItemType), arguments.At(1));
		//					var itemBase = InventoryItemLoader.AvailableItems[itemtype];
		//					var pickup = UnityEngine.Object.Instantiate(itemBase.PickupDropModel,
		//						new UnityEngine.Vector3(float.Parse(arguments.At(2)), float.Parse(arguments.At(3)), float.Parse(arguments.At(4))),
		//						Quaternion.Euler(Vector3.up * float.Parse(arguments.At(5))));
		//					pickup.Info.ItemId = itemtype;
		//					pickup.Info.Weight = itemBase.Weight;
		//					pickup.Info.Locked = true;
		//					pickup.GetComponent<Rigidbody>().useGravity = false;
		//					pickup.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(6)), float.Parse(arguments.At(7)), float.Parse(arguments.At(8)));

		//					targetitem = pickup;
		//					ItemDistributor.SpawnPickup(pickup);
		//				}
		//				else
		//				{
		//					NetworkServer.Destroy(targetitem.gameObject);
		//					targetitem = null;
		//				}
		//				response = $"itemtest.";
		//				return true;
		//			}
		//		case "worktest":
		//			{
		//				if(targetstation == null)
		//				{
		//					var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Station"));
		//					var station = UnityEngine.Object.Instantiate(prefab, 
		//						new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))), 
		//						Quaternion.Euler(Vector3.up * float.Parse(arguments.At(4))));
		//					station.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(5)), float.Parse(arguments.At(6)), float.Parse(arguments.At(7)));
		//					targetstation = station;
		//					NetworkServer.Spawn(station);
		//				}
		//				else
		//				{
		//					NetworkServer.Destroy(targetstation);
		//					targetstation = null;
		//				}
		//				response = $"worktest.";
		//				return true;
		//			}
		//		case "doortest":
		//			{
		//				if(targetdoor == null)
		//				{
		//					var prefab = UnityEngine.Object.FindObjectsOfType<DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("HCZ"));
		//					var door = UnityEngine.Object.Instantiate(prefab.TargetPrefab, new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3))), Quaternion.Euler(Vector3.up * 180f));
		//					door.transform.localScale = new UnityEngine.Vector3(float.Parse(arguments.At(4)), float.Parse(arguments.At(5)), float.Parse(arguments.At(6)));
		//					targetdoor = door;
		//					NetworkServer.Spawn(door.gameObject);
		//				}
		//				else
		//				{
		//					NetworkServer.Destroy(targetdoor.gameObject);
		//					targetdoor = null;
		//				}
		//				response = $"doortest.";
		//				return true;
		//			}
		//		case "lightcolor":
		//			{
		//				if(arguments.Count == 1)
		//				{
		//					response = "Usage: lightcolor r g b or lightcolor reset";
		//					return false;
		//				}

		//				if(arguments.Count == 2 && arguments.At(1) == "reset")
		//				{
		//					foreach(var i in FlickerableLightController.Instances)
		//					{
		//						i.WarheadLightColor = FlickerableLightController.DefaultWarheadColor;
		//						i.WarheadLightOverride = false;
		//					}
		//					response = "reset ok.";
		//					return true;
		//				}

		//				if(arguments.Count == 4 
		//					&& float.TryParse(arguments.At(1), out var r) 
		//					&& float.TryParse(arguments.At(2), out var g) 
		//					&& float.TryParse(arguments.At(3), out var b))
		//				{
		//					foreach(var i in FlickerableLightController.Instances)
		//					{
		//						i.WarheadLightColor = new Color(r / 255f, g / 255f, b / 255f);
		//						i.WarheadLightOverride = true;
		//					}
		//					response = $"color set:{r},{g},{b}";
		//					return true;
		//				}
		//				response = $"lightcolor: invalid params.";
		//				return false;
		//			}
		//		case "sinkhole":
		//			{
		//				if(SanyaPlugin.Instance.Handlers.Sinkhole == null)
		//				{
		//					response = "Sinkhole is null.";
		//					return false;
		//				}
		//				else
		//				{
		//					Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkhole, player.Position);
		//					response = "ok.";
		//					return true;
		//				}
		//			}
		//		case "frag":
		//			{
		//				var target = Player.Get(int.Parse(arguments.At(1)));

		//				if(target != null)
		//				{
		//					Methods.SpawnGrenade(target.Position, ItemType.GrenadeHE, -1, target.ReferenceHub);
		//					response = $"{target.Nickname} ok.";
		//					return true;
		//				}
		//				else
		//				{
		//					response = $"target is null.";
		//					return false;
		//				}
		//			}
		//		case "flash":
		//			{
		//				var target = Player.Get(int.Parse(arguments.At(1)));

		//				if(target != null)
		//				{
		//					Methods.SpawnGrenade(target.Position, ItemType.GrenadeFlash, -1, target.ReferenceHub);
		//					response = $"{target.Nickname} ok.";
		//					return true;
		//				}
		//				else
		//				{
		//					response = $"target is null.";
		//					return false;
		//				}
		//			}
		//		case "ball":
		//			{
		//				var target = Player.Get(int.Parse(arguments.At(1)));

		//				if(target != null)
		//				{
		//					Methods.SpawnGrenade(target.Position + Vector3.up, ItemType.SCP018, -1, target.ReferenceHub);
		//					response = $"{target.Nickname} ok.";
		//					return true;
		//				}
		//				else
		//				{
		//					response = $"target is null.";
		//					return false;
		//				}
		//			}
		//		case "scale":
		//			{
		//				var target = Player.Get(int.Parse(arguments.At(1)));

		//				target.Scale = new UnityEngine.Vector3(
		//					float.Parse(arguments.At(2)),
		//					float.Parse(arguments.At(3)),
		//					float.Parse(arguments.At(4))
		//				);

		//				response = $"{target.Nickname} ok.";
		//				return true;
		//			}
		//		case "args":
		//			{
		//				response = "ok.\n";
		//				for(int i = 0; i < arguments.Count; i++)
		//				{
		//					response += $"[{i}]{arguments.At(i)}\n";
		//				}
		//				response = response.TrimEnd('\n');
		//				return true;
		//			}
		//		case "htest":
		//			{
		//				foreach(var i in Player.List)
		//					i.SendTextHintNotEffect(arguments.Skip(1).Join(delimiter: " ").Replace("\\n", "\n"), 5);
		//				response = "ok.";
		//				return true;
		//			}
		//		case "hud":
		//			{
		//				var comp = player.GameObject.GetComponent<SanyaPluginComponent>();
		//				response = $"ok.{comp.DisableHud} -> ";
		//				comp.DisableHud = !comp.DisableHud;
		//				response += $"{comp.DisableHud}";
		//				return true;
		//			}
		//		case "hudall":
		//			{
		//				foreach(var i in Player.List)
		//					if(i.ReferenceHub.TryGetComponent<SanyaPluginComponent>(out var sanya))
		//						sanya.DisableHud = bool.Parse(arguments.At(1));
		//				response = $"set to {bool.Parse(arguments.At(1))}";
		//				return true;
		//			}
		//		case "ping":
		//			{
		//				response = "Pings:\n";

		//				foreach(var ply in Player.List)
		//				{
		//					response += $"{ply.Nickname} : {LiteNetLib4MirrorServer.Peers[ply.Connection.connectionId].Ping}ms\n";
		//				}
		//				return true;
		//			}
		//		case "acwh":
		//			{
		//				response = "ok.";
		//				player.ReferenceHub.playerMovementSync.NoclipWhitelisted = true;
		//				return true;
		//			}
		//		case "106":
		//			{
		//				foreach(var pocketteleport in UnityEngine.Object.FindObjectsOfType<PocketDimensionTeleport>())
		//				{
		//					pocketteleport.SetType(PocketDimensionTeleport.PDTeleportType.Exit);
		//				}
		//				response = "ok.";
		//				return true;
		//			}
		//		case "recipes":
		//			{
		//				response = "SCP-914 Recipes:\n";

		//				foreach(var itemtype in Enum.GetValues(typeof(ItemType)))
		//					if(Scp914Upgrader.TryGetProcessor((ItemType)itemtype, out var processor))
		//					{
		//						if(processor is StandardItemProcessor standard)
		//						{
		//							response += $"{itemtype}:\n";

		//							response += $"    Rough:\n";
		//							foreach(var i in standard._roughOutputs)
		//								response += $"        {i}\n";

		//							response += $"    Coarse:\n";
		//							foreach(var i in standard._coarseOutputs)
		//								response += $"        {i}\n";

		//							response += $"    1to1:\n";
		//							foreach(var i in standard._oneToOneOutputs)
		//								response += $"        {i}\n";

		//							response += $"    Fine:\n";
		//							foreach(var i in standard._fineOutputs)
		//								response += $"        {i}\n";

		//							response += $"    VeryFine:\n";
		//							foreach(var i in standard._veryFineOutputs)
		//								response += $"        {i}\n";

		//							response = response.TrimEnd('\n');
		//						}
		//						else if(processor is AmmoItemProcessor ammo)
		//						{
		//							response += $"{itemtype}:\n";

		//							response += $"    Rough:\n";
		//							response += $"        {ammo._previousAmmo}\n";

		//							response += $"    Coarse:\n";
		//							response += $"        {ammo._previousAmmo}\n";

		//							response += $"    1to1:\n";
		//							response += $"        {ammo._oneToOne}\n";

		//							response += $"    Fine:\n";
		//							response += $"        {ammo._nextAmmo}\n";

		//							response += $"    VeryFine:\n";
		//							response += $"        {ammo._nextAmmo}\n";

		//							response = response.TrimEnd('\n');
		//						}
		//						else if(processor is FirearmItemProcessor firearm)
		//						{
		//							response += $"{itemtype}:\n";

		//							response += $"    Rough:\n";
		//							foreach(var i in firearm._roughOutputs)
		//							{
		//								response += $"        [{i.Chance * 100f}]\n";
		//								foreach(var o in i.TargetItems)
		//									response += $"           {o}\n";
		//							}

		//							response += $"    Coarse:\n";
		//							foreach(var i in firearm._coarseOutputs)
		//							{
		//								response += $"        [{i.Chance * 100f}]\n";
		//								foreach(var o in i.TargetItems)
		//									response += $"           {o}\n";
		//							}

		//							response += $"    1to1:\n";
		//							response += $"        {itemtype}\n";

		//							response += $"    Fine:\n";
		//							foreach(var i in firearm._fineOutputs)
		//							{
		//								response += $"        [{i.Chance * 100f}]\n";
		//								foreach(var o in i.TargetItems)
		//									response += $"           {o}\n";
		//							}

		//							response += $"    VeryFine:\n";
		//							foreach(var i in firearm._veryFineOutputs)
		//							{
		//								response += $"        [{i.Chance * 100f}]\n";
		//								foreach(var o in i.TargetItems)
		//									response += $"           {o}\n";
		//							}

		//							response = response.TrimEnd('\n');
		//						}
		//						response += "\n";
		//					}
		//				return true;
		//			}
		//		case "nukecap":
		//			{
		//				var outsite = UnityEngine.Object.FindObjectOfType<AlphaWarheadOutsitePanel>();
		//				response = $"ok.[{outsite.keycardEntered}] -> ";
		//				outsite.NetworkkeycardEntered = !outsite.keycardEntered;
		//				response += $"[{outsite.keycardEntered}]";
		//				return true;
		//			}
		//		case "nukelock":
		//			{
		//				response = $"ok.[{AlphaWarheadController.Host._isLocked}] -> ";
		//				AlphaWarheadController.Host._isLocked = !AlphaWarheadController.Host._isLocked;
		//				response += $"[{AlphaWarheadController.Host._isLocked}]";
		//				return true;
		//			}
		//		case "lure":
		//			{
		//				var lure = UnityEngine.Object.FindObjectOfType<LureSubjectContainer>();
		//				response = $"ok.[{lure.allowContain}] -> ";
		//				lure.NetworkallowContain = !lure.allowContain;
		//				response += $"[{lure.allowContain}]";
		//				return true;
		//			}
		//		case "femur":
		//			{
		//				ReferenceHub.HostHub.playerInteract.RpcContain106(null);
		//				response = "ok.";
		//				return true;
		//			}
		//		case "addscps":
		//			{
		//				response = $"ok.{RoundSummary.singleton.classlistStart.scps_except_zombies} -> ";
		//				RoundSummary.singleton.classlistStart.scps_except_zombies++;
		//				response += $"[{RoundSummary.singleton.classlistStart.scps_except_zombies}]";
		//				return true;
		//			}
		//		case "forceend":
		//			{
		//				RoundSummary.singleton.ForceEnd();
		//				response = "Force Ended!";
		//				return true;
		//			}
		//		case "now":
		//			{
		//				response = $"now ticks:{TimeBehaviour.CurrentTimestamp()}";
		//				return true;
		//			}
		//		case "resetflag":
		//			{
		//				response = "ok.";
		//				PlayerDataManager.ReloadParams();
		//				return true;
		//			}
		//		case "configs":
		//			{
		//				response = Methods.ToStringPropertiesAndFields(SanyaPlugin.Instance.Config);
		//				return true;
		//			}
		//		default:
		//			{
		//				response = "invalid params.";
		//				return false;
		//			}
		//	}
		//}

	}
}
