using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CustomPlayerEffects;
using EXILED;
using EXILED.Extensions;
using Grenades;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MEC;
using Mirror;
using PlayableScps;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using SanyaPlugin.Patches;
using UnityEngine;
using Utf8Json;

namespace SanyaPlugin
{
	public class EventHandlers
	{
		public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
		internal readonly SanyaPlugin plugin;
		internal List<CoroutineHandle> roundCoroutines = new List<CoroutineHandle>();
		internal bool loaded = false;

		/** Infosender **/
		private readonly UdpClient udpClient = new UdpClient();
		internal Task sendertask;
		private bool senderdisabled = false;
		internal async Task SenderAsync()
		{
			Log.Debug($"[Infosender_Task] Started.");

			while(true)
			{
				try
				{
					if(Configs.infosender_ip == "none")
					{
						Log.Info($"[Infosender_Task] Disabled(config:({Configs.infosender_ip}). breaked.");
						senderdisabled = true;
						break;
					}

					if(!this.loaded)
					{
						Log.Debug($"[Infosender_Task] Plugin not loaded. Skipped...");
						await Task.Delay(TimeSpan.FromSeconds(30));
					}

					Serverinfo cinfo = new Serverinfo();

					DateTime dt = DateTime.Now;
					cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
					cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
					cinfo.modversion = $"{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}";
					cinfo.sanyaversion = SanyaPlugin.Version;
					cinfo.gamemode = eventmode.ToString();
					cinfo.name = ServerConsole.singleton.RefreshServerName();
					cinfo.ip = ServerConsole.Ip;
					cinfo.port = ServerConsole.Port;
					cinfo.playing = PlayerManager.players.Count;
					cinfo.maxplayer = CustomNetworkManager.slots;
					cinfo.duration = RoundSummary.roundTime;

					if(cinfo.playing > 0)
					{
						foreach(GameObject player in PlayerManager.players)
						{
							Playerinfo ply = new Playerinfo
							{
								name = ReferenceHub.GetHub(player).nicknameSync.MyNick,
								userid = ReferenceHub.GetHub(player).characterClassManager.UserId,
								ip = ReferenceHub.GetHub(player).queryProcessor._ipAddress,
								role = ReferenceHub.GetHub(player).characterClassManager.CurClass.ToString(),
								rank = ReferenceHub.GetHub(player).serverRoles.MyText
							};

							cinfo.players.Add(ply);
						}
					}

					string json = JsonSerializer.ToJsonString(cinfo);

					byte[] sendBytes = Encoding.UTF8.GetBytes(json);
					udpClient.Send(sendBytes, sendBytes.Length, Configs.infosender_ip, Configs.infosender_port);
					Log.Debug($"[Infosender_Task] {Configs.infosender_ip}:{Configs.infosender_port}");
				}
				catch(Exception e)
				{
					throw e;
				}
				await Task.Delay(TimeSpan.FromSeconds(30));
			}
		}

		/** AuthChecker **/
		internal const byte BypassFlags = (1 << 1) | (1 << 3);
		internal static readonly NetDataReader reader = new NetDataReader();
		internal static readonly NetDataWriter writer = new NetDataWriter();
		internal static readonly Dictionary<string, string> kickedbyChecker = new Dictionary<string, string>();

		/** Update **/
		internal IEnumerator<float> EverySecond()
		{
			while(true)
			{
				try
				{
					//自動空爆
					if(Configs.outsidezone_termination_time_after_nuke > 0
						&& detonatedDuration != -1
						&& RoundSummary.roundTime > (Configs.outsidezone_termination_time_after_nuke + detonatedDuration))
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb()));
						detonatedDuration = -1;
					}

					//寝返り
					if(Configs.traitor_limitter > 0)
					{
						foreach(var player in Player.GetHubs())
						{
							if((player.GetTeam() == Team.MTF || player.GetTeam() == Team.CHI)
								&& player.IsHandCuffed()
								&& Vector3.Distance(espaceArea, player.transform.position) <= Escape.radius
								&& RoundSummary.singleton.CountTeam(player.GetTeam()) <= Configs.traitor_limitter)
							{
								switch(player.GetTeam())
								{
									case Team.MTF:
										if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
										{
											Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : MTF->CHI");
											player.characterClassManager.SetPlayersClass(RoleType.ChaosInsurgency, player.gameObject);
										}
										else
										{
											Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : Traitor Failed(by percent)");
											player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
										}
										break;
									case Team.CHI:
										if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
										{
											Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : CHI->MTF");
											player.characterClassManager.SetPlayersClass(RoleType.NtfCadet, player.gameObject);
										}
										else
										{
											Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : Traitor Failed(by percent)");
											player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
										}
										break;
								}
							}
						}
					}

					//ItemCleanup
					if(Configs.item_cleanup > 0)
					{
						List<GameObject> nowitems = null;

						foreach(var i in ItemCleanupPatch.items)
						{
							if(Time.time - i.Value > Configs.item_cleanup && i.Key != null)
							{
								if(nowitems == null) nowitems = new List<GameObject>();
								Log.Debug($"[ItemCleanupPatch] Cleanup:{i.Key.transform.position} {Time.time - i.Value} > {Configs.item_cleanup}");
								nowitems.Add(i.Key);
							}
						}

						if(nowitems != null)
						{
							foreach(var x in nowitems)
							{
								ItemCleanupPatch.items.Remove(x);
								NetworkServer.Destroy(x);
							}
						}
					}

					//停電時強制再収用の際復電
					if(eventmode == SANYA_GAME_MODE.NIGHT && IsEnableBlackout && Generator079.mainGenerator.forcedOvercharge)
					{
						IsEnableBlackout = false;
					}

					//SCP-079's Spot Humans
					if(Configs.scp079_spot)
					{
						foreach(var scp079 in Scp079PlayerScript.instances)
						{
							if(scp079.iAm079)
							{
								foreach(var player in Player.GetHubs())
								{
									if(player.characterClassManager.IsHuman() && scp079.currentCamera.CanLookToPlayer(player))
									{
										player.playerStats.TargetBloodEffect(player.playerStats.connectionToClient, Vector3.zero, 0.1f);
										foreach(var scp in Player.GetHubs(Team.SCP))
										{
											// NEXT
										}
									}
								}
							}
						}
					}
				}
				catch(Exception e)
				{
					Log.Error($"[EverySecond] {e}");
				}
				//毎秒
				yield return Timing.WaitForSeconds(1f);
			}
		}
		internal IEnumerator<float> FixedUpdate()
		{
			while(true)
			{
				try
				{
					//Blackouter
					if(flickerableLight != null && IsEnableBlackout && flickerableLight.remainingFlicker < 0f && !flickerableLight.IsDisabled())
					{
						//Log.Debug($"{UnityEngine.Object.FindObjectOfType<FlickerableLight>().remainingFlicker}");
						Log.Debug($"[Blackouter] Fired.");
						Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
					}
				}
				catch(Exception e)
				{
					Log.Error($"[FixedUpdate] {e}");
				}
				//FixedUpdateの次フレームへ
				yield return Timing.WaitForOneFrame;
			}
		}

		/** Flag Params **/
		private int detonatedDuration = -1;
		private Vector3 espaceArea = new Vector3(177.5f, 985.0f, 29.0f);
		private readonly int grenade_pickup_mask = 1049088;
		//private readonly int surfacemask = 1208303617;

		/** RoundVar **/
		private FlickerableLight flickerableLight = null;
		private bool IsEnableBlackout = false;

		/** EventModeVar **/
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		private Vector3 LCZArmoryPos;

		public void OnWaintingForPlayers()
		{
			loaded = true;

			if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation && !senderdisabled)
				sendertask = SenderAsync().StartSender();

			roundCoroutines.Add(Timing.RunCoroutine(EverySecond(), Segment.FixedUpdate));
			roundCoroutines.Add(Timing.RunCoroutine(FixedUpdate(), Segment.FixedUpdate));

			PlayerDataManager.playersData.Clear();
			ItemCleanupPatch.items.Clear();
			Coroutines.isAirBombGoing = false;

			detonatedDuration = -1;
			IsEnableBlackout = false;

			flickerableLight = UnityEngine.Object.FindObjectOfType<FlickerableLight>();

			if(Configs.tesla_range != 5.5f)
			{
				foreach(var tesla in UnityEngine.Object.FindObjectsOfType<TeslaGate>())
				{
					tesla.sizeOfTrigger = Configs.tesla_range;
				}
			}

			eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(Configs.event_mode_weight.ToArray());
			switch(eventmode)
			{
				case SANYA_GAME_MODE.NIGHT:
					{
						break;
					}
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						foreach(var room in Map.Rooms)
						{
							if(room.Name == "LCZ_Armory")
							{
								LCZArmoryPos = room.Position + new Vector3(0, 2, 0);
							}
						}
						break;
					}
				default:
					{
						eventmode = SANYA_GAME_MODE.NORMAL;
						break;
					}
			}


			Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
		}

		public void OnRoundStart()
		{
			Log.Info($"[OnRoundStart] Round Start!");

			switch(eventmode)
			{
				case SANYA_GAME_MODE.NIGHT:
					{
						IsEnableBlackout = true;
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.StartNightMode()));
						break;
					}
			}
		}

		public void OnRoundEnd()
		{
			Log.Info($"[OnRoundEnd] Round Ended.");

			if(Configs.data_enabled)
			{
				foreach(ReferenceHub player in Player.GetHubs())
				{
					if(string.IsNullOrEmpty(player.GetUserId())) continue;

					if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
					{
						if(player.GetRole() == RoleType.Spectator)
						{
							PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_other);
						}
						else
						{
							PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_win);
						}
					}
				}

				foreach(var data in PlayerDataManager.playersData.Values)
				{
					data.lastUpdate = DateTime.Now;
					data.playingcount++;
					PlayerDataManager.SavePlayerData(data);
				}
			}

			if(Configs.godmode_after_endround)
			{
				foreach(var player in Player.GetHubs())
				{
					player.characterClassManager.GodMode = true;
				}
			}

			Coroutines.isAirBombGoing = false;
		}

		public void OnRoundRestart()
		{
			Log.Info($"[OnRoundRestart] Restarting...");

			foreach(var cor in roundCoroutines)
				Timing.KillCoroutines(cor);
			roundCoroutines.Clear();
		}

		public void OnWarheadStart(WarheadStartEvent ev)
		{
			Log.Debug($"[OnWarheadStart] {ev.Player?.GetNickname()}");

			if(Configs.cassie_subtitle)
			{
				bool isresumed = AlphaWarheadController._resumeScenario != -1;
				double left = isresumed ? AlphaWarheadController.Host.timeToDetonation : AlphaWarheadController.Host.timeToDetonation - 4;
				double count = Math.Truncate(left / 10.0) * 10.0;

				if(!isresumed)
				{
					Methods.SendSubtitle(Subtitles.AlphaWarheadStart.Replace("{0}", count.ToString()), 15);
				}
				else
				{
					Methods.SendSubtitle(Subtitles.AlphaWarheadResume.Replace("{0}", count.ToString()), 10);
				}
			}
		}

		public void OnWarheadCancel(WarheadCancelEvent ev)
		{
			Log.Debug($"[OnWarheadCancel] {ev.Player?.GetNickname()}");

			if(AlphaWarheadController.Host._isLocked) return;

			if(Configs.cassie_subtitle)
			{
				Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
			}

			if(Configs.close_doors_on_nukecancel)
			{
				foreach(var door in UnityEngine.Object.FindObjectsOfType<Door>())
				{
					if(door.warheadlock)
					{
						if(door.isOpen)
						{
							door.RpcDoSound();
						}
						door.moving.moving = true;
						door.SetState(false);
					}
				}
			}
		}

		public void OnDetonated()
		{
			Log.Debug($"[OnDetonated] Detonated:{RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}");

			detonatedDuration = RoundSummary.roundTime;

			if(Configs.stop_respawn_after_detonated)
			{
				PlayerManager.localPlayer.GetComponent<MTFRespawn>().SummonChopper(false);
			}
		}

		public void OnAnnounceDecont(AnnounceDecontaminationEvent ev)
		{
			Log.Debug($"[OnAnnounceDecont] {ev.AnnouncementId} {DecontaminationController.Singleton._stopUpdating}");

			if(Configs.cassie_subtitle)
			{
				ev.IsAnnouncementGlobal = true;
				switch(ev.AnnouncementId)
				{
					case 0:
						{
							Methods.SendSubtitle(Subtitles.DecontaminationInit, 20);
							break;
						}
					case 1:
						{
							Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "10"), 15);
							break;
						}
					case 2:
						{
							Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "5"), 15);
							break;
						}
					case 3:
						{
							Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "1"), 15);
							break;
						}
					case 4:
						{
							Methods.SendSubtitle(Subtitles.Decontamination30s, 45);
							break;
						}
					case 5:
						{
							//no announce
							break;
						}
					case 6:
						{
							Methods.SendSubtitle(Subtitles.DecontaminationLockdown, 15);
							break;
						}
				}
			}
		}

		public void OnPreAuth(ref PreauthEvent ev)
		{
			Log.Debug($"[OnPreAuth] {ev.Request.RemoteEndPoint.Address}:{ev.UserId}");

			if((Configs.kick_steam_limited || Configs.kick_vpn) && !ev.UserId.Contains("@northwood", StringComparison.InvariantCultureIgnoreCase))
			{
				reader.SetSource(ev.Request.Data.RawData, 20);
				if(reader.TryGetBytesWithLength(out var b) && reader.TryGetString(out var s) &&
					reader.TryGetULong(out var e) && reader.TryGetByte(out var flags))
				{
					if((flags & BypassFlags) > 0)
					{
						Log.Warn($"[OnPreAuth] User have bypassflags. {ev.UserId}");
						return;
					}
				}
			}

			if(Configs.data_enabled && !PlayerDataManager.playersData.ContainsKey(ev.UserId))
			{
				PlayerDataManager.playersData.Add(ev.UserId, PlayerDataManager.LoadPlayerData(ev.UserId));
			}

			if(Configs.kick_vpn)
			{
				if(ShitChecker.IsBlacklisted(ev.Request.RemoteEndPoint.Address))
				{
					ev.Allow = false;
					writer.Reset();
					writer.Put((byte)10);
					writer.Put(Subtitles.VPNKickMessageShort);
					ev.Request.Reject(writer);
					return;
				}

				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckVPN(ev)));
			}

			if(Configs.kick_steam_limited && ev.UserId.Contains("@steam", StringComparison.InvariantCultureIgnoreCase))
			{
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckIsLimitedSteam(ev.UserId)));
			}
		}

		public void OnPlayerJoin(PlayerJoinEvent ev)
		{
			if(ev.Player.IsHost()) return;
			Log.Info($"[OnPlayerJoin] {ev.Player.GetNickname()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

			if(kickedbyChecker.TryGetValue(ev.Player.GetUserId(), out var reason))
			{
				string reasonMessage = string.Empty;
				if(reason == "steam")
					reasonMessage = Subtitles.LimitedKickMessage;
				else if(reason == "vpn")
					reasonMessage = Subtitles.VPNKickMessage;

				ServerConsole.Disconnect(ev.Player.characterClassManager.connectionToClient, reasonMessage);
				kickedbyChecker.Remove(ev.Player.GetUserId());
				return;
			}

			if(!string.IsNullOrEmpty(Configs.motd_message))
			{
				Methods.SendSubtitle(Configs.motd_message.Replace("[name]", ev.Player.GetNickname()), 10, ev.Player);
			}

			if(Configs.data_enabled
				&& Configs.level_enabled
				&& PlayerDataManager.playersData.TryGetValue(ev.Player.GetUserId(), out PlayerData data))
			{
				Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate);
			}

			if(Configs.disable_all_chat)
			{
				if(!(Configs.disable_chat_bypass_whitelist && WhiteList.IsOnWhitelist(ev.Player.GetUserId())))
				{
					ev.Player.characterClassManager.NetworkMuted = true;
				}
			}

			//MuteFixer
			foreach(ReferenceHub player in Player.GetHubs())
				if(player.IsMuted())
					player.characterClassManager.SetDirtyBit(1uL);

			//SpeedFixer
			ServerConfigSynchronizer.Singleton.SetDirtyBit(2uL);
			ServerConfigSynchronizer.Singleton.SetDirtyBit(4uL);
		}

		public void OnPlayerLeave(PlayerLeaveEvent ev)
		{
			if(ev.Player.IsHost()) return;
			Log.Debug($"[OnPlayerLeave] {ev.Player.GetNickname()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

			if(Configs.data_enabled && !string.IsNullOrEmpty(ev.Player.GetUserId()))
			{
				if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
				{
					PlayerDataManager.playersData.Remove(ev.Player.GetUserId());
				}
			}
		}

		public void OnStartItems(StartItemsEvent ev)
		{
			if(ev.Player.IsHost()) return;
			Log.Debug($"[OnStartItems] {ev.Player.GetNickname()} -> {ev.Role}");

			if(Configs.defaultitems.TryGetValue(ev.Role, out List<ItemType> itemconfig) && itemconfig.Count > 0)
			{
				ev.StartItems = itemconfig;
			}

			if(itemconfig != null && itemconfig.Contains(ItemType.None))
			{
				ev.StartItems.Clear();
			}

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.Role == RoleType.ClassD && Configs.classd_insurgency_classd_inventory.Count > 0)
						{
							ev.StartItems = Configs.classd_insurgency_classd_inventory;
						}
						if(ev.Role == RoleType.Scientist && Configs.classd_insurgency_scientist_inventory.Count > 0)
						{
							ev.StartItems = Configs.classd_insurgency_scientist_inventory;
						}
						break;
					}
			}
		}

		public void OnPlayerSetClass(SetClassEvent ev)
		{
			if(ev.Player.IsHost()) return;
			Log.Debug($"[OnPlayerSetClass] {ev.Player.GetNickname()} -> {ev.Role}");

			if(Configs.scp079_ex_enabled && ev.Role == RoleType.Scp079)
			{
				roundCoroutines.Add(Timing.CallDelayed(10f, () => ev.Player.SendTextHint(HintTexts.Extend079First, 10)));
			}

			if(Configs.recovery_amount_scp049 > 0 && ev.Role == RoleType.Scp0492)
			{
				foreach(var scp049 in RoleType.Scp049.GetHubs())
				{
					scp049.playerStats.HealHPAmount(Configs.recovery_amount_scp049);
				}
			}
		}

		public void OnPlayerSpawn(PlayerSpawnEvent ev)
		{
			if(ev.Player.IsHost()) return;
			Log.Debug($"[OnPlayerSpawn] {ev.Player.GetNickname()} -{ev.Role}-> {ev.Spawnpoint}");

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.Role == RoleType.ClassD)
						{
							ev.Spawnpoint = LCZArmoryPos;
						}
						break;
					}
			}
		}

		public void OnPlayerHurt(ref PlayerHurtEvent ev)
		{
			if(ev.Player.IsHost() || ev.Player.GetRole() == RoleType.Spectator || ev.Player.characterClassManager.GodMode || ev.Player.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnPlayerHurt:Before] {ev.Attacker?.GetNickname()}[{ev.Attacker?.GetRole()}] -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");

			if(ev.Attacker == null) return;

			if(ev.DamageType != DamageTypes.Nuke
				&& ev.DamageType != DamageTypes.Decont
				&& ev.DamageType != DamageTypes.Wall
				&& ev.DamageType != DamageTypes.Tesla
				&& ev.DamageType != DamageTypes.Scp207)
			{
				//GrenadeHitmark
				if(Configs.grenade_hitmark
					&& ev.DamageType == DamageTypes.Grenade
					&& ev.Player.GetUserId() != ev.Attacker.GetUserId())
				{
					ev.Attacker.ShowHitmarker();
				}

				//USPMultiplier
				if(ev.DamageType == DamageTypes.Usp)
				{
					if(ev.Player.characterClassManager.IsAnyScp())
					{
						ev.Amount *= Configs.damage_usp_multiplier_scp;
					}
					else
					{
						ev.Amount *= Configs.damage_usp_multiplier_human;
					}
					ev.Player.playerEffectsController.EnableEffect<Deafened>(3f);
					ev.Player.playerEffectsController.EnableEffect<Blinded>(3f);
				}

				//939Bleeding
				if(Configs.scp939_attack_bleeding && ev.DamageType == DamageTypes.Scp939)
				{
					ev.Player.playerEffectsController.EnableEffect<Bleeding>();
					ev.Player.playerEffectsController.EnableEffect<Hemorrhage>();
				}

				//049-2Effect
				if(Configs.scp0492_hurt_effect && ev.DamageType == DamageTypes.Scp0492)
				{
					ev.Player.playerEffectsController.EnableEffect<Blinded>(2f);
					ev.Player.playerEffectsController.EnableEffect<Amnesia>(2f);
				}

				//HurtBlink173
				if(Configs.scp173_hurt_blink_percent > 0 && ev.Player.GetRole() == RoleType.Scp173 && UnityEngine.Random.Range(0, 100) < Configs.scp173_hurt_blink_percent)
				{
					Methods.Blink();
				}

				//CuffedDivisor
				if(ev.Player.IsHandCuffed())
				{
					ev.Amount /= Configs.damage_divisor_cuffed;
				}

				//SCPsDivisor
				if(ev.DamageType != DamageTypes.MicroHid)
				{
					switch(ev.Player.GetRole())
					{
						case RoleType.Scp173:
							ev.Amount /= Configs.damage_divisor_scp173;
							break;
						case RoleType.Scp106:
							if(ev.DamageType == DamageTypes.Grenade) ev.Amount /= Configs.damage_divisor_scp106_grenade;
							ev.Amount /= Configs.damage_divisor_scp106;
							break;
						case RoleType.Scp049:
							ev.Amount /= Configs.damage_divisor_scp049;
							break;
						case RoleType.Scp096:
							ev.Amount /= Configs.damage_divisor_scp096;
							break;
						case RoleType.Scp0492:
							ev.Amount /= Configs.damage_divisor_scp0492;
							break;
						case RoleType.Scp93953:
						case RoleType.Scp93989:
							ev.Amount /= Configs.damage_divisor_scp939;
							break;
					}
				}
			}

			Log.Debug($"[OnPlayerHurt:After] {ev.Attacker?.GetNickname()}[{ev.Attacker?.GetRole()}] -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");
		}

		public void OnPlayerDeath(ref PlayerDeathEvent ev)
		{
			if(ev.Player.IsHost() || ev.Player.GetRole() == RoleType.Spectator || ev.Player.characterClassManager.GodMode || ev.Player.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnPlayerDeath] {ev.Killer?.GetNickname()}[{ev.Killer?.GetRole()}] -{ev.Info.GetDamageName()}-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");

			if(ev.Killer == null) return;

			if(Configs.data_enabled)
			{
				if(!string.IsNullOrEmpty(ev.Killer.GetUserId())
					&& ev.Player.GetUserId() != ev.Killer.GetUserId()
					&& PlayerDataManager.playersData.ContainsKey(ev.Killer.GetUserId()))
				{
					PlayerDataManager.playersData[ev.Killer.GetUserId()].AddExp(Configs.level_exp_kill);
				}

				if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
				{
					PlayerDataManager.playersData[ev.Player.GetUserId()].AddExp(Configs.level_exp_death);
				}
			}

			if(ev.Info.GetDamageType() == DamageTypes.Scp173 && ev.Killer.GetRole() == RoleType.Scp173 && Configs.recovery_amount_scp173 > 0)
			{
				ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp173);
			}
			if(ev.Info.GetDamageType() == DamageTypes.Scp096 && ev.Killer.GetRole() == RoleType.Scp096 && Configs.recovery_amount_scp096 > 0)
			{
				ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp096);
			}
			if(ev.Info.GetDamageType() == DamageTypes.Scp939 && (ev.Killer.GetRole() == RoleType.Scp93953 || ev.Killer.GetRole() == RoleType.Scp93989) && Configs.recovery_amount_scp939 > 0)
			{
				ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp939);
				ev.Player.inventory.Clear();
			}
			if(ev.Info.GetDamageType() == DamageTypes.Scp0492 && ev.Killer.GetRole() == RoleType.Scp0492 && Configs.recovery_amount_scp0492 > 0)
			{
				ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp0492);
			}

			if(Configs.kill_hitmark
				&& ev.Killer.GetTeam() != Team.SCP
				&& !string.IsNullOrEmpty(ev.Killer.GetUserId())
				&& ev.Killer.GetUserId() != ev.Player.GetUserId())
			{
				Timing.RunCoroutine(Coroutines.BigHitmark(ev.Killer.GetComponent<MicroHID>()));
			}

			if(Configs.cassie_subtitle
				&& ev.Player.GetTeam() == Team.SCP
				&& ev.Player.GetRole() != RoleType.Scp0492
				&& ev.Player.GetRole() != RoleType.Scp079)
			{
				string fullname = CharacterClassManager._staticClasses.Get(ev.Player.GetRole()).fullName;
				string str;
				if(ev.Info.GetDamageType() == DamageTypes.Tesla)
				{
					str = Subtitles.SCPDeathTesla.Replace("{0}", fullname);
				}
				else if(ev.Info.GetDamageType() == DamageTypes.Nuke)
				{
					str = Subtitles.SCPDeathWarhead.Replace("{0}", fullname);
				}
				else if(ev.Info.GetDamageType() == DamageTypes.Decont)
				{
					str = Subtitles.SCPDeathDecont.Replace("{0}", fullname);
				}
				else
				{
					Team killerTeam = ev.Killer.GetTeam();
					foreach(var i in Player.GetHubs())
					{
						if(i.queryProcessor.PlayerId == ev.Info.PlayerId)
						{
							killerTeam = i.GetTeam();
						}
					}
					Log.Debug($"[CheckTeam] ply:{ev.Player.queryProcessor.PlayerId} kil:{ev.Killer.queryProcessor.PlayerId} plyid:{ev.Info.PlayerId} killteam:{killerTeam}");

					if(killerTeam == Team.CDP)
					{
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "Dクラス職員").Replace("{2}", "Class-D Personnel");
					}
					else if(killerTeam == Team.CHI)
					{
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "カオス・インサージェンシー").Replace("{2}", "Chaos Insurgency");
					}
					else if(killerTeam == Team.RSC)
					{
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "研究員").Replace("{2}", "Science Personnel");
					}
					else if(killerTeam == Team.MTF)
					{
						string unit = NineTailedFoxUnits.host.list[ev.Killer.characterClassManager.NtfUnit];
						str = Subtitles.SCPDeathContainedMTF.Replace("{0}", fullname).Replace("{1}", unit);
					}
					else
					{
						str = Subtitles.SCPDeathUnknown.Replace("{0}", fullname);
					}
				}

				int count = 0;
				bool isFound079 = false;
				bool isForced = false;
				foreach(var i in Player.GetHubs())
				{
					if(ev.Player.GetUserId() == i.GetUserId()) continue;
					if(i.GetTeam() == Team.SCP) count++;
					if(i.GetRole() == RoleType.Scp079) isFound079 = true;
				}

				Log.Debug($"[Check079] SCPs:{count} isFound079:{isFound079} totalvol:{Generator079.mainGenerator.totalVoltage} forced:{Generator079.mainGenerator.forcedOvercharge}");
				if(count == 1
					&& isFound079
					&& Generator079.mainGenerator.totalVoltage < 4
					&& !Generator079.mainGenerator.forcedOvercharge
					&& ev.Info.GetDamageType() != DamageTypes.Nuke)
				{
					isForced = true;
					str = str.Replace("{-1}", "\n全てのSCPオブジェクトの安全が確保されました。SCP-079の再収用手順を開始します。\n重度収用区画は約一分後にオーバーチャージされます。").Replace("{-2}", "\nAll SCP subject has been secured. SCP-079 recontainment sequence commencing.\nHeavy containment zone will overcharge in t-minus 1 minutes.");
				}
				else
				{
					str = str.Replace("{-1}", string.Empty).Replace("{-2}", string.Empty);
				}

				Methods.SendSubtitle(str, (ushort)(isForced ? 30 : 10));
			}

			if(ev.Info.GetDamageType() == DamageTypes.Tesla || ev.Info.GetDamageType() == DamageTypes.Nuke)
			{
				ev.Player.inventory.Clear();
			}

			//Ticket Extend
			switch(ev.Player.GetTeam())
			{
				case Team.CDP:
					Cassie.mtfRespawn.ChaosRespawnTickets += Configs.tickets_ci_classd_died_count;
					if(ev.Killer.GetTeam() == Team.MTF || ev.Killer.GetTeam() == Team.RSC) Cassie.mtfRespawn.MtfRespawnTickets += Configs.tickets_mtf_classd_killed_count;
					break;
				case Team.RSC:
					Cassie.mtfRespawn.MtfRespawnTickets += Configs.tickets_mtf_scientist_died_count;
					if(ev.Killer.GetTeam() == Team.CHI || ev.Killer.GetTeam() == Team.CDP) Cassie.mtfRespawn.ChaosRespawnTickets += Configs.tickets_ci_scientist_killed_count;
					break;
				case Team.MTF:
					if(ev.Killer.GetTeam() == Team.SCP || ev.Killer.GetTeam() == Team.CDP) Cassie.mtfRespawn.MtfRespawnTickets += Configs.tickets_mtf_killed_by_enemy_count;
					break;
				case Team.CHI:
					if(ev.Killer.GetTeam() == Team.SCP || ev.Killer.GetTeam() == Team.RSC) Cassie.mtfRespawn.ChaosRespawnTickets += Configs.tickets_ci_killed_by_enemy_count;
					break;
			}
		}

		public void OnPocketDimDeath(PocketDimDeathEvent ev)
		{
			Log.Debug($"[OnPocketDimDeath] {ev.Player.GetNickname()}");

			if(Configs.data_enabled)
			{
				foreach(ReferenceHub player in Player.GetHubs())
				{
					if(player.GetRole() == RoleType.Scp106)
					{
						if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
						{
							PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_kill);
						}
					}
				}
			}

			if(Configs.recovery_amount_scp106 > 0)
			{
				foreach(ReferenceHub player in Player.GetHubs())
				{
					if(player.GetRole() == RoleType.Scp106)
					{
						player.playerStats.HealHPAmount(Configs.recovery_amount_scp106);
						player.ShowHitmarker();
					}
				}
			}
		}

		public void OnPlayerUsedMedicalItem(UsedMedicalItemEvent ev)
		{
			Log.Debug($"[OnPlayerUsedMedicalItem] {ev.Player.GetNickname()} -> {ev.ItemType}");

			if(ev.ItemType == ItemType.Medkit || ev.ItemType == ItemType.SCP500)
			{
				ev.Player.playerEffectsController.DisableEffect<Hemorrhage>();
				ev.Player.playerEffectsController.DisableEffect<Bleeding>();
			}
		}

		public void OnPlayerTriggerTesla(ref TriggerTeslaEvent ev)
		{
			if(Configs.tesla_triggerable_teams.Count == 0
				|| Configs.tesla_triggerable_teams.Contains((int)ev.Player.GetTeam()))
			{
				if(Configs.tesla_triggerable_disarmed || ev.Player.handcuffs.CufferId == -1)
				{
					ev.Triggerable = true;
				}
				else
				{
					ev.Triggerable = false;
				}
			}
			else
			{
				ev.Triggerable = false;
			}
		}

		public void OnPlayerDoorInteract(ref DoorInteractionEvent ev)
		{
			Log.Debug($"[OnPlayerDoorInteract] {ev.Player.GetNickname()}:{ev.Door.DoorName}:{ev.Door.PermissionLevels}");

			if(Configs.inventory_keycard_act && ev.Player.GetTeam() != Team.SCP && !ev.Player.serverRoles.BypassMode && !ev.Door.locked)
			{
				foreach(var item in ev.Player.inventory.items)
				{
					foreach(var permission in ev.Player.inventory.GetItemByID(item.id).permissions)
					{
						if(ev.Door.backwardsCompatPermissions.TryGetValue(permission, out var flag) && ev.Door.PermissionLevels.HasPermission(flag))
						{
							ev.Allow = true;
						}
					}
				}
			}

			//Mini fix
			if(ev.Door.DoorName.Contains("CHECKPOINT") && (ev.Door.decontlock || ev.Door.warheadlock) && !ev.Door.isOpen)
			{
				ev.Door.SetStateWithSound(true);
			}
		}

		public void OnPlayerLockerInteract(LockerInteractionEvent ev)
		{
			Log.Debug($"[OnPlayerLockerInteract] {ev.Player.GetNickname()}:{ev.LockerId}");
			if(Configs.inventory_keycard_act)
			{
				foreach(var item in ev.Player.inventory.items)
				{
					if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("PEDESTAL_ACC"))
					{
						ev.Allow = true;
					}
				}
			}
		}

		public void OnPlayerChangeAnim(ref SyncDataEvent ev)
		{
			if(ev.Player.IsHost() || ev.Player.animationController.curAnim == ev.State) return;

			if(Configs.scp079_ex_enabled && ev.Player.GetRole() == RoleType.Scp079)
			{
				if(ev.State == 1)
					ev.Player.SendTextHint(HintTexts.Extend079Enabled, 5);
				else
					ev.Player.SendTextHint(HintTexts.Extend079Disabled, 5);
			}

			if(Configs.stamina_jump_used != -1f
				&& ev.State == 2 
				&& ev.Player.characterClassManager.IsHuman() 
				&& !ev.Player.fpc.staminaController._invigorated.Enabled 
				&& !ev.Player.fpc.staminaController._scp207.Enabled
				)
			{
				ev.Player.fpc.staminaController.RemainingStamina -= Configs.stamina_jump_used;
				ev.Player.fpc.staminaController._regenerationTimer = 0f;

				if(ev.Player.fpc.staminaController.RemainingStamina <= 0f)
				{
					ev.Player.playerEffectsController.EnableEffect<Disabled>(7f);
					ev.Player.playerEffectsController.EnableEffect<Concussed>(5f);
				}
			}
		}

		public void OnTeamRespawn(ref TeamRespawnEvent ev)
		{
			Log.Debug($"[OnTeamRespawn] Queues:{ev.ToRespawn.Count} IsCI:{ev.IsChaos} MaxAmount:{ev.MaxRespawnAmt}");

			if(Configs.stop_respawn_after_detonated && AlphaWarheadController.Host.detonated)
			{
				ev.ToRespawn.Clear();
			}

			if(Configs.godmode_after_endround && !RoundSummary.RoundInProgress())
			{
				ev.ToRespawn.Clear();
			}
		}

		public void OnGeneratorUnlock(ref GeneratorUnlockEvent ev)
		{
			Log.Debug($"[OnGeneratorUnlock] {ev.Player.GetNickname()} -> {ev.Generator.CurRoom}");
			if(Configs.inventory_keycard_act && !ev.Player.serverRoles.BypassMode)
			{
				foreach(var item in ev.Player.inventory.items)
				{
					if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("ARMORY_LVL_2"))
					{
						ev.Allow = true;
					}
				}
			}

			if(ev.Allow && Configs.generator_unlock_to_open)
			{
				ev.Generator._doorAnimationCooldown = 1.5f;
				ev.Generator.NetworkisDoorOpen = true;
				ev.Generator.RpcDoSound(true);
			}
		}

		public void OnGeneratorOpen(ref GeneratorOpenEvent ev)
		{
			Log.Debug($"[OnGeneratorOpen] {ev.Player.GetNickname()} -> {ev.Generator.CurRoom}");
			if(ev.Generator.prevFinish && Configs.generator_finish_to_lock) ev.Allow = false;
		}

		public void OnGeneratorClose(ref GeneratorCloseEvent ev)
		{
			Log.Debug($"[OnGeneratorClose] {ev.Player.GetNickname()} -> {ev.Generator.CurRoom}");
			if(ev.Allow && ev.Generator.isTabletConnected && Configs.generator_activating_opened) ev.Allow = false;
		}

		public void OnGeneratorInsert(ref GeneratorInsertTabletEvent ev)
		{
			Log.Debug($"[OnGeneratorInsert] {ev.Player.GetNickname()} -> {ev.Generator.CurRoom}");
		}

		public void OnGeneratorFinish(ref GeneratorFinishEvent ev)
		{
			Log.Debug($"[OnGeneratorFinish] {ev.Generator.CurRoom}");
			if(Configs.generator_finish_to_lock) ev.Generator.NetworkisDoorOpen = false;

			int curgen = Generator079.mainGenerator.NetworktotalVoltage + 1;
			if(Configs.cassie_subtitle && !Generator079.mainGenerator.forcedOvercharge)
			{
				if(curgen < 5)
				{
					Methods.SendSubtitle(Subtitles.GeneratorFinish.Replace("{0}", curgen.ToString()), 10);
				}
				else
				{
					Methods.SendSubtitle(Subtitles.GeneratorComplete, 20);
				}
			}

			if(eventmode == SANYA_GAME_MODE.NIGHT && curgen >= 3 && IsEnableBlackout)
			{
				IsEnableBlackout = false;
			}
		}

		public void On079LevelGain(Scp079LvlGainEvent ev)
		{
			Log.Debug($"[On079LevelGain] {ev.Player.GetNickname()}");

			if(Configs.scp079_ex_enabled)
			{
				switch(ev.OldLvl)
				{
					case 1:
						ev.Player.SendTextHint(HintTexts.Extend079Lv2, 10);
						break;
					case 2:
						ev.Player.SendTextHint(HintTexts.Extend079Lv3, 10);
						break;
					case 3:
						ev.Player.SendTextHint(HintTexts.Extend079Lv4, 10);
						break;
				}
			}
		}

		public void On106MakePortal(Scp106CreatedPortalEvent ev)
		{
			Log.Debug($"[On106MakePortal] {ev.Player.GetNickname()}:{ev.PortalPosition}:{ev.Player.IsExmode()}");

			//var scp106 = ev.Player.GetComponent<Scp106PlayerScript>();
			//Vector3 backvec = new Vector3(ev.Player.PlayerCameraReference.forward.x, 0f, ev.Player.PlayerCameraReference.forward.z);
			//if(!scp106.goingViaThePortal && ev.Player.falldamage.isGrounded && ev.Player.IsExmode())
			//{
			//	if(Physics.Raycast(ev.Player.GetPosition(), ev.Player.PlayerCameraReference.forward, out RaycastHit raycastHit, 500f, surfacemask)
			//		&& Physics.Raycast(raycastHit.point, -Vector3.up, out RaycastHit raycastHit1, 500f, surfacemask))
			//	{
			//		ev.PortalPosition = (raycastHit1.point - backvec) - Vector3.up;
			//	}
			//}
		}

		public void On106Teleport(Scp106TeleportEvent ev)
		{
			Log.Debug($"[On106Teleport] {ev.Player.GetNickname()}:{ev.PortalPosition}:{ev.Player.IsExmode()}");
		}

		public void On914Upgrade(ref SCP914UpgradeEvent ev)
		{
			Log.Debug($"[On914Upgrade] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}");

			if(Configs.scp914_intake_death)
			{
				foreach(var player in ev.Players)
				{
					player.inventory.Clear();
					var info = new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0);
					player.playerStats.HurtPlayer(info, player.gameObject);
				}
			}
		}

		public void OnShoot(ref ShootEvent ev)
		{
			Log.Debug($"[OnShoot] {ev.Shooter.GetNickname()} -{ev.TargetPos}-> {ev.Target?.name}");

			if((Configs.grenade_shoot_fuse || Configs.item_shoot_move)
				&& ev.TargetPos != Vector3.zero
				&& Physics.Linecast(ev.Shooter.GetPosition(), ev.TargetPos, out RaycastHit raycastHit, grenade_pickup_mask))
			{
				if(Configs.item_shoot_move)
				{
					var pickup = raycastHit.transform.GetComponentInParent<Pickup>();
					if(pickup != null && pickup.Rb != null)
					{
						pickup.Rb.AddExplosionForce(Vector3.Distance(ev.TargetPos, ev.Shooter.GetPosition()), ev.Shooter.GetPosition(), 500f, 3f, ForceMode.Impulse);
					}
				}

				if(Configs.grenade_shoot_fuse)
				{
					var grenade = raycastHit.transform.GetComponentInParent<FragGrenade>();
					if(grenade != null)
					{
						grenade.NetworkfuseTime = 0.1f;
					}
				}
			}
		}

		public void OnCommand(ref RACommandEvent ev)
		{
			string[] args = ev.Command.Split(' ');
			Log.Debug($"[OnCommand] sender:{ev.Sender.SenderId} command:{ev.Command} args:{args.Length}");

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
		}
	}
}