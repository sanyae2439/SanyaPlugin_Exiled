using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using RemoteAdmin;
using SanyaPlugin.Functions;

namespace SanyaPlugin.Commands
{
	[CommandHandler(typeof(GameConsoleCommandHandler))]
	[CommandHandler(typeof(RemoteAdminCommandHandler))]
	class Commands : ICommand
	{
		public string Command { get; } = "sanya";

		public string[] Aliases { get; } = new string[] { "sn" };

		public string Description { get; } = "SanyaPlugin Commands";

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
				response = "sanya plugins command.";
				return true;
			}

			switch(arguments.FirstElement().ToLower())
			{
				case "test":
					{
						response = "test ok.";
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
				case "actwatch":
					{
						player.SendCustomSync(player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) => {
							writer.WritePackedUInt64(1ul);
							writer.WritePackedUInt32((uint)1);
							writer.WriteByte((byte)SyncList<byte>.Operation.OP_SET);
							writer.WritePackedUInt32((uint)3);
							writer.WriteByte((byte)1);
						}, null);
						response = "ok.";
						return true;
					}
				case "addscps":
					{
						response = $"ok.{RoundSummary.singleton.classlistStart.scps_except_zombies++}";
						return true;
					}
				case "forceend":
					{
						RoundSummary.singleton.ForceEnd();
						response = "Force Ended!";
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

			/*
			if(args[0].ToLower() == "sanya")
			{
				ReferenceHub player = ev.Sender.SenderId == "SERVER CONSOLE" || ev.Sender.SenderId == "GAME CONSOLE" ? PlayerManager.localPlayer.GetPlayer() : Player.GetPlayer(ev.Sender.SenderId);
				if(!player.CheckPermission("sanya.racommand"))
				{
					ev.Allow = false;
					ev.Sender.RAMessage("Permission denied.", false);
					return;
				}

				if(args.Length > 1)
				{
					string ReturnStr;
					bool isSuccess = true;
					switch(args[1].ToLower())
					{
						case "test":
							{
								ReturnStr = "test ok.";
								break;
							}
						case "resynceffect":
							{
								foreach(var ply in Player.GetHubs())
								{
									ply.playerEffectsController.Resync();
								}
								ReturnStr = "Resync ok.";
								break;
							}
						case "check":
							{
								ReturnStr = $"Players List ({PlayerManager.players.Count})\n";
								foreach(var i in Player.GetHubs())
								{
									ReturnStr += $"{i.GetNickname()} {i.GetPosition()}\n";
									foreach(var effect in i.playerEffectsController.syncEffectsIntensity)
										ReturnStr += $"{effect}";
									ReturnStr += "\n";
								}
								ReturnStr.Trim();
								break;
							}
						case "showconfig":
							{
								ReturnStr = Configs.GetConfigs();
								break;
							}
						case "reload":
							{
								Plugin.Config.Reload();
								Configs.Reload();
								if(Configs.kick_vpn) ShitChecker.LoadLists();
								ReturnStr = "reload ok";
								break;
							}
						case "list":
							{
								ReturnStr = $"Players List ({PlayerManager.players.Count})\n";
								foreach(var i in Player.GetHubs())
								{
									ReturnStr += $"[{i.GetPlayerId()}]{i.GetNickname()}({i.GetUserId()})<{i.GetRole()}/{i.GetHealth()}HP> {i.GetPosition()}\n";
								}
								ReturnStr.Trim();
								break;
							}
						case "startair":
							{
								roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb()));
								ReturnStr = "Started!";
								break;
							}
						case "stopair":
							{
								ReturnStr = $"Stop ok. now:{Coroutines.isAirBombGoing}";
								Coroutines.isAirBombGoing = false;
								break;
							}
						case "dummy":
							{
								if(player != null)
								{
									var gameObject = Methods.SpawnDummy(player.GetRole(), player.GetPosition(), player.transform.rotation);
									ReturnStr = $"{player.GetRole()}'s Dummy Created. pos:{gameObject.transform.position} rot:{gameObject.transform.rotation}";
								}
								else
								{
									isSuccess = false;
									ReturnStr = "sender should be Player.";
								}
								break;
							}
						case "106":
							{
								foreach(PocketDimensionTeleport pdt in UnityEngine.Object.FindObjectsOfType<PocketDimensionTeleport>())
								{
									pdt.SetType(PocketDimensionTeleport.PDTeleportType.Exit);
								}
								ReturnStr = "All set to [Exit].";
								break;
							}
						case "096":
							{
								foreach(var i in Player.GetHubs())
								{
									if(i.GetRole() == RoleType.Scp096)
									{
										if(i.scpsController.curScp is Scp096 scp096)
										{
											scp096.Windup(true);
										}
									}
								}
								ReturnStr = "096 enraged!";
								break;
							}
						case "914":
							{
								if(args.Length > 2)
								{
									if(!Scp914.Scp914Machine.singleton.working)
									{

										if(args[2] == "use")
										{
											Scp914.Scp914Machine.singleton.RpcActivate(NetworkTime.time);
											ReturnStr = $"Used : {Scp914.Scp914Machine.singleton.knobState}";
										}
										else if(args[2] == "knob")
										{
											Scp914.Scp914Machine.singleton.ChangeKnobStatus();
											ReturnStr = $"Knob Changed to:{Scp914.Scp914Machine.singleton.knobState}";
										}
										else
										{
											isSuccess = false;
											ReturnStr = "[914] Wrong Parameters.";
										}
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[914] SCP-914 is working now.";
									}
								}
								else
								{
									isSuccess = false;
									ReturnStr = "[914] Parameters : 914 <use/knob>";
								}
								break;
							}
						case "nukecap":
							{
								var outsite = GameObject.Find("OutsitePanelScript")?.GetComponent<AlphaWarheadOutsitePanel>();
								outsite.NetworkkeycardEntered = !outsite.keycardEntered;
								ReturnStr = $"{outsite?.keycardEntered}";
								break;
							}
						case "sonar":
							{
								if(player == null)
								{
									ReturnStr = $"Source not found. (Cant use from SERVER)";
								}
								else
								{
									int counter = 0;
									foreach(var target in Player.GetHubs())
									{
										if(player.IsEnemy(target.GetTeam()))
										{
											// NEXT
											counter++;
										}
									}
									ReturnStr = $"Sonar Activated : {counter}";
								}
								break;
							}
						case "blackout":
							{
								if(args.Length > 2 && args[2] == "hcz")
								{
									Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, true);
									ReturnStr = "HCZ blackout!";
								}
								else
								{
									Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
									ReturnStr = "ALL blackout!";
								}
								break;
							}
						case "femur":
							{
								PlayerManager.localPlayer.GetComponent<PlayerInteract>()?.RpcContain106(PlayerManager.localPlayer);
								ReturnStr = "FemurScreamer!";
								break;
							}
						case "explode":
							{
								if(args.Length > 2)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null && target.GetRole() != RoleType.Spectator)
									{
										Methods.SpawnGrenade(target.transform.position, false, 0.1f, target);
										ReturnStr = $"success. target:{target.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[explode] missing target.";
									}
								}
								else
								{
									if(player != null)
									{
										Methods.SpawnGrenade(player.transform.position, false, 0.1f, player);
										ReturnStr = $"success. target:{player.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[explode] missing target.";
									}
								}
								break;
							}
						case "grenade":
							{
								if(args.Length > 2)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null && target.GetRole() != RoleType.Spectator)
									{
										Methods.SpawnGrenade(target.transform.position, false, -1, target);
										ReturnStr = $"success. target:{target.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[grenade] missing target.";
									}
								}
								else
								{
									if(player != null)
									{
										Methods.SpawnGrenade(player.transform.position, false, -1, player);
										ReturnStr = $"success. target:{player.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[grenade] missing target.";
									}
								}
								break;
							}
						case "flash":
							{
								if(args.Length > 2)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null && target.GetRole() != RoleType.Spectator)
									{
										Methods.SpawnGrenade(target.transform.position, true, -1, target);
										ReturnStr = $"success. target:{target.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[flash] missing target.";
									}
								}
								else
								{
									if(player != null)
									{
										Methods.SpawnGrenade(player.transform.position, true, -1, player);
										ReturnStr = $"success. target:{player.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[flash] missing target.";
									}
								}
								break;
							}
						case "ball":
							{
								if(args.Length > 2)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null && target.GetRole() != RoleType.Spectator)
									{
										Methods.Spawn018(target);
										ReturnStr = $"success. target:{target.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[ball] missing target.";
									}
								}
								else
								{
									if(player != null)
									{
										Methods.Spawn018(player);
										ReturnStr = $"success. target:{player.GetNickname()}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[ball] missing target.";
									}
								}
								break;
							}
						case "ammo":
							{
								if(player != null)
								{
									for(int i = 0; i < player.ammoBox.amount.Count; i++)
									{
										player.ammoBox.amount[i] = 200U;
									}
									ReturnStr = "Ammo set 200:200:200.";
								}
								else
								{
									ReturnStr = "Failed to set. (cant use from SERVER)";
								}

								break;
							}
						case "ev":
							{
								foreach(Lift lift in UnityEngine.Object.FindObjectsOfType<Lift>())
								{
									lift.UseLift();
								}
								ReturnStr = "EV Used.";
								break;
							}
						case "roompos":
							{
								string output = "\n";
								foreach(var rid in UnityEngine.Object.FindObjectsOfType<Rid>())
								{
									output += $"{rid.id} : {rid.transform.position}\n";
								}
								ReturnStr = output;
								break;
							}
						case "tppos":
							{
								if(args.Length > 5)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null)
									{
										if(float.TryParse(args[3], out float x)
											&& float.TryParse(args[4], out float y)
											&& float.TryParse(args[5], out float z))
										{
											Vector3 pos = new Vector3(x, y, z);
											target.playerMovementSync.OverridePosition(pos, 0f, true);
											ReturnStr = $"TP to {pos}.";
										}
										else
										{
											isSuccess = false;
											ReturnStr = "[tppos] Wrong parameters.";
										}
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[tppos] missing target.";
									}
								}
								else
								{
									isSuccess = false;
									ReturnStr = "[tppos] parameters : tppos <player> <x> <y> <z>";
								}

								break;
							}
						case "pocket":
							{
								if(args.Length > 2)
								{
									ReferenceHub target = Player.GetPlayer(args[2]);
									if(target != null)
									{
										// next
										ReturnStr = $"target[{target.GetNickname()}] move to PocketDimension.";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[pocket] missing target.";
									}
								}
								else
								{
									if(player != null)
									{
										// next
										ReturnStr = $"target[{player.GetNickname()}] move to PocketDimension.";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[pocket] missing target.";
									}
								}
								break;
							}
						case "gen":
							{
								if(args.Length > 2)
								{
									if(args[2] == "unlock")
									{
										foreach(var generator in Generator079.Generators)
										{
											generator.NetworkisDoorUnlocked = true;
											generator.NetworkisDoorOpen = true;
											generator._doorAnimationCooldown = 0.5f;
										}
										ReturnStr = "gen unlocked.";
									}
									else if(args[2] == "door")
									{
										foreach(var generator in Generator079.Generators)
										{
											if(!generator.prevFinish)
											{
												bool now = !generator.isDoorOpen;
												generator.NetworkisDoorOpen = now;
												generator.CallRpcDoSound(now);
											}
										}
										ReturnStr = $"gen doors interacted.";
									}
									else if(args[2] == "set")
									{
										float cur = 10f;
										foreach(var generator in Generator079.Generators)
										{
											if(!generator.prevFinish)
											{
												generator.NetworkisDoorOpen = true;
												generator.NetworkisTabletConnected = true;
												generator.NetworkremainingPowerup = cur;
												cur += 10f;
											}
										}
										ReturnStr = "gen set.";
									}
									else if(args[2] == "once")
									{
										Generator079 gen = Generator079.Generators.FindAll(x => !x.prevFinish).GetRandomOne();

										if(gen != null)
										{
											gen.NetworkisDoorUnlocked = true;
											gen.NetworkisTabletConnected = true;
											gen.NetworkisDoorOpen = true;
										}
										ReturnStr = "set once.";
									}
									else if(args[2] == "eject")
									{
										foreach(var generator in Generator079.Generators)
										{
											if(generator.isTabletConnected)
											{
												generator.EjectTablet();
											}
										}
										ReturnStr = "gen ejected.";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[gen] Wrong Parameters.";
									}
								}
								else
								{
									isSuccess = false;
									ReturnStr = "[gen] Parameters : gen <unlock/door/set/once/eject>";
								}
								break;
							}
						case "spawn":
							{
								var mtfrespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
								if(mtfrespawn.nextWaveIsCI)
								{
									mtfrespawn.timeToNextRespawn = 14f;
								}
								else
								{
									mtfrespawn.timeToNextRespawn = 18.5f;
								}
								ReturnStr = $"spawn soon. nextIsCI:{mtfrespawn.nextWaveIsCI}";
								break;
							}
						case "next":
							{
								if(args.Length > 2)
								{
									MTFRespawn mtfRespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
									if(args[2] == "ci")
									{
										mtfRespawn.nextWaveIsCI = true;
										ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
									}
									else if(args[2] == "mtf" || args[2] == "ntf")
									{
										mtfRespawn.nextWaveIsCI = false;
										ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
									}
									else
									{
										isSuccess = false;
										ReturnStr = "[next] Wrong Parameters.";
									}
								}
								else
								{
									isSuccess = false;
									ReturnStr = "[next] Wrong Parameters.";
								}
								break;
							}
						case "van":
							{
								PlayerManager.localPlayer.GetComponent<MTFRespawn>()?.RpcVan();
								ReturnStr = "Van Called!";
								break;
							}
						case "heli":
							{
								MTFRespawn mtf_r = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
								mtf_r.SummonChopper(!mtf_r._mtfA.isLanded);
								ReturnStr = "Heli Called!";
								break;
							}
						case "now":
							{
								ReturnStr = TimeBehaviour.CurrentTimestamp().ToString();
								break;
							}
						default:
							{
								ReturnStr = "Wrong Parameters.";
								isSuccess = false;
								break;
							}
					}
					ev.Allow = false;
					ev.Sender.RAMessage(ReturnStr, isSuccess);
				}
				else
				{
					ev.Allow = false;
					ev.Sender.RAMessage(string.Concat(
						"Usage : sanya < reload / startair / stopair / nukelock / list / blackout ",
						"/ roompos / tppos / pocket / gen / spawn / next / van / heli / 106 / 096 / 914 / now / ammo / test >"
						), false);
				}
			}
			*/
		}
	}
}
