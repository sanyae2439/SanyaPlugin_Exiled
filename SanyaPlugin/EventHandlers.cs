﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;
using LiteNetLib.Utils;
using MEC;
using Utf8Json;
using CustomPlayerEffects;
using Exiled.Events;
using Exiled.Events.EventArgs;
using Exiled.API.Features;
using Exiled.API.Extensions;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using SanyaPlugin.Patches;

namespace SanyaPlugin
{
	public class EventHandlers
	{
		public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
		public readonly SanyaPlugin plugin;
		public List<CoroutineHandle> roundCoroutines = new List<CoroutineHandle>();
		private bool loaded = false;

		/** Infosender **/
		private readonly UdpClient udpClient = new UdpClient();
		internal Task sendertask;
		internal async Task SenderAsync()
		{
			Log.Debug($"[SenderAsync] Started.", SanyaPlugin.instance.Config.IsDebugged);

			while(true)
			{
				try
				{
					if(plugin.Config.InfosenderIp == "none" || plugin.Config.InfosenderPort == -1)
					{
						Log.Info($"[SenderAsync] Disabled(config:({plugin.Config.InfosenderIp}:{plugin.Config.InfosenderPort}). breaked.");
						break;
					}

					if(!this.loaded)
					{
						Log.Debug($"[SenderAsync] Plugin not loaded. Skipped...", SanyaPlugin.instance.Config.IsDebugged);
						await Task.Delay(TimeSpan.FromSeconds(30));
					}

					Serverinfo cinfo = new Serverinfo();

					DateTime dt = DateTime.Now;
					cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
					cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
					cinfo.modversion = Events.Instance.Version.ToString();
					cinfo.sanyaversion = SanyaPlugin.instance.Version.ToString();
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
					udpClient.Send(sendBytes, sendBytes.Length, plugin.Config.InfosenderIp, plugin.Config.InfosenderPort);
					Log.Debug($"[SenderAsync] {plugin.Config.InfosenderIp}:{plugin.Config.InfosenderPort}", SanyaPlugin.instance.Config.IsDebugged);
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
		private IEnumerator<float> EverySecond()
		{
			while(true)
			{
				try
				{
					//自動空爆
					if(plugin.Config.OutsidezoneTerminationTimeAfterNuke > 0
						&& detonatedDuration != -1
						&& RoundSummary.roundTime > (plugin.Config.OutsidezoneTerminationTimeAfterNuke + detonatedDuration))
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb()));
						detonatedDuration = -1;
					}

					//寝返り
					if(plugin.Config.TraitorLimit > 0)
					{
						foreach(var player in Player.List)
						{
							if((player.Team == Team.MTF || player.Team == Team.CHI)
								&& player.IsCuffed
								&& Vector3.Distance(espaceArea, player.Position) <= Escape.radius
								&& RoundSummary.singleton.CountTeam(player.Team) <= plugin.Config.TraitorLimit)
							{
								int hit = UnityEngine.Random.Range(0, 100);

								if(hit >= plugin.Config.TraitorChancePercent)
								{
									switch(player.Team)
									{
										case Team.MTF:
											Log.Info($"[Traitor] {player.Nickname} : MTF->CHI");
											player.SetRole(RoleType.ChaosInsurgency);
											break;
										case Team.CHI:
											Log.Info($"[Traitor] {player.Nickname} : CHI->MTF");
											player.SetRole(RoleType.NtfCadet);
											break;
									}
								}
								else
								{
									Log.Info($"[Traitor] {player.Nickname} : Traitor Failed ({hit} <= {plugin.Config.TraitorChancePercent})");
									player.SetRole(RoleType.Spectator);
								}
							}
						}
					}

					//ItemCleanup
					if(plugin.Config.ItemCleanup > 0)
					{
						List<GameObject> nowitems = null;

						foreach(var i in ItemCleanupPatch.items)
						{
							if(Time.time - i.Value > plugin.Config.ItemCleanup && i.Key != null)
							{
								if(nowitems == null) nowitems = new List<GameObject>();
								Log.Debug($"[ItemCleanup] Cleanup:{i.Key.transform.position} {Time.time - i.Value} > {plugin.Config.ItemCleanup}", SanyaPlugin.instance.Config.IsDebugged);
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
					/*
					if(plugin.Config.Scp079Spot)
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
					*/
				}
				catch(Exception e)
				{
					Log.Error($"[EverySecond] {e}");
				}
				//毎秒
				yield return Timing.WaitForSeconds(1f);
			}
		}
		private IEnumerator<float> FixedUpdate()
		{
			while(true)
			{
				try
				{
					//Blackouter
					if(flickerableLight != null && IsEnableBlackout && flickerableLight.remainingFlicker < 0f && !flickerableLight.IsDisabled())
					{
						Log.Debug($"[Blackouter] Fired.", SanyaPlugin.instance.Config.IsDebugged);
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
		//private readonly int grenade_pickup_mask = 1049088;
		//private readonly int surfacemask = 1208303617;

		/** RoundVar **/
		private FlickerableLight flickerableLight = null;
		private bool IsEnableBlackout = false;

		/** EventModeVar **/
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		private Vector3 LCZArmoryPos;

		//ServerEvents
		public void OnWaintingForPlayers()
		{
			loaded = true;
			plugin.RegistNameFormatter();

			if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation
				&& plugin.Config.InfosenderIp != "none" && plugin.Config.InfosenderPort != -1)
				sendertask = SenderAsync().StartSender();

			roundCoroutines.Add(Timing.RunCoroutine(EverySecond(), Segment.FixedUpdate));
			roundCoroutines.Add(Timing.RunCoroutine(FixedUpdate(), Segment.FixedUpdate));

			PlayerDataManager.playersData.Clear();
			ItemCleanupPatch.items.Clear();
			Coroutines.isAirBombGoing = false;

			detonatedDuration = -1;
			IsEnableBlackout = false;

			flickerableLight = UnityEngine.Object.FindObjectOfType<FlickerableLight>();

			if(plugin.Config.TeslaRange != 5.5f)
			{
				foreach(var tesla in Map.TeslaGates)
				{
					tesla.sizeOfTrigger = plugin.Config.TeslaRange;
				}
			}

			eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(plugin.Config.EventModeWeight.ToArray());
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
		public void OnRoundStarted()
		{
			Log.Info($"[OnRoundStarted] Round Start!");

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
		public void OnRoundEnded(RoundEndedEventArgs ev)
		{
			Log.Info($"[OnRoundEnded] Round Ended.");

			if(plugin.Config.DataEnabled)
			{
				foreach(var player in Player.List)
				{
					if(string.IsNullOrEmpty(player.UserId)) continue;

					if(PlayerDataManager.playersData.ContainsKey(player.UserId))
					{
						if(player.Role == RoleType.Spectator)
							PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpLose);
						else
							PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpWin);
					}
				}

				foreach(var data in PlayerDataManager.playersData.Values)
				{
					data.lastUpdate = DateTime.Now;
					data.playingcount++;
					PlayerDataManager.SavePlayerData(data);
				}
			}

			if(plugin.Config.GodmodeAfterEndround)
				foreach(var player in Player.List)
					player.IsGodModeEnabled = true;

			Coroutines.isAirBombGoing = false;
		}
		public void OnRestartingRound()
		{
			Log.Info($"[OnRestartingRound] Restarting...");

			foreach(var cor in roundCoroutines)
				Timing.KillCoroutines(cor);
			roundCoroutines.Clear();
		}
		public void OnRespawningTeam(RespawningTeamEventArgs ev)
		{
			Log.Debug($"[OnRespawningTeam] Queues:{ev.Players.Count} IsCI:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.StopRespawnAfterDetonated && Warhead.IsDetonated
				|| plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
			{
				ev.Players.Clear();
			}
		}
		public void OnLocalReporting(LocalReportingEventArgs ev)
		{
			Log.Debug($"[OnLocalReporting] {ev.Issuer.Nickname} -{ev.Reason}-> {ev.Target.Nickname}", SanyaPlugin.instance.Config.IsDebugged);

			if(!string.IsNullOrEmpty(plugin.Config.ReportWebhook) && ev.Issuer != ev.Target)
			{
				Log.Warn($"[OnLocalReporting] {ev.Issuer.Nickname} -{ev.Reason}-> {ev.Issuer.Nickname}");
				Methods.SendReport(ev.Target, ev.Reason, ev.Issuer);
			}
		}

		//MapEvents
		public void OnAnnouncingDecontamination(AnnouncingDecontaminationEventArgs ev)
		{
			Log.Debug($"[OnAnnouncingDecontamination] {ev.Id}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.CassieSubtitle)
				switch(ev.Id)
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
				}
		}
		public void OnDecontaminating(DecontaminatingEventArgs ev)
		{
			Log.Debug($"[OnDecontaminating]", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.DecontaminationLockdown, 15);
		}
		public void OnGeneratorActivated(GeneratorActivatedEventArgs ev)
		{
			Log.Debug($"[OnGeneratorActivated] {ev.Generator.CurRoom}", SanyaPlugin.instance.Config.IsDebugged);
			if(plugin.Config.GeneratorFinishLock) ev.Generator.NetworkisDoorOpen = false;

			int curgen = Generator079.mainGenerator.NetworktotalVoltage + 1;
			if(plugin.Config.CassieSubtitle && !Generator079.mainGenerator.forcedOvercharge)
				if(curgen < 5)
					Methods.SendSubtitle(Subtitles.GeneratorFinish.Replace("{0}", curgen.ToString()), 10);
				else
					Methods.SendSubtitle(Subtitles.GeneratorComplete, 20);

			if(eventmode == SANYA_GAME_MODE.NIGHT && curgen >= 3 && IsEnableBlackout)
				IsEnableBlackout = false;
		}

		//WarheadEvents
		public void OnStarting(StartingEventArgs ev)
		{
			Log.Debug($"[OnStarting] {ev.Player.Nickname}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.CassieSubtitle)
			{
				bool isresumed = AlphaWarheadController._resumeScenario != -1;
				double left = isresumed ? AlphaWarheadController.Host.timeToDetonation : AlphaWarheadController.Host.timeToDetonation - 4;
				double count = Math.Truncate(left / 10.0) * 10.0;

				if(!isresumed)
					Methods.SendSubtitle(Subtitles.AlphaWarheadStart.Replace("{0}", count.ToString()), 15);
				else
					Methods.SendSubtitle(Subtitles.AlphaWarheadResume.Replace("{0}", count.ToString()), 10);
			}
		}
		public void OnStopping(StoppingEventArgs ev)
		{
			Log.Debug($"[OnStopping] {ev.Player.Nickname}", SanyaPlugin.instance.Config.IsDebugged);

			if(AlphaWarheadController.Host._isLocked) return;

			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);

			if(plugin.Config.CloseDoorsOnNukecancel)
				foreach(var door in Map.Doors)
					if(door.warheadlock)
						door.SetStateWithSound(false);
		}
		public void OnDetonated()
		{
			Log.Info($"[OnDetonated] Detonated:{RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}");

			detonatedDuration = RoundSummary.roundTime;
		}

		//PlayerEvents
		public void OnPreAuthenticating(PreAuthenticatingEventArgs ev)
		{
			Log.Debug($"[OnPreAuthenticating] {ev.Request.RemoteEndPoint.Address}:{ev.UserId}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled && !PlayerDataManager.playersData.ContainsKey(ev.UserId))
				PlayerDataManager.playersData.Add(ev.UserId, PlayerDataManager.LoadPlayerData(ev.UserId));

			if((plugin.Config.KickSteamLimited || plugin.Config.KickVpn) && !ev.UserId.Contains("@northwood", StringComparison.InvariantCultureIgnoreCase))
			{
				reader.SetSource(ev.Request.Data.RawData, 20);
				if(reader.TryGetBytesWithLength(out var b) && reader.TryGetString(out var s) &&
					reader.TryGetULong(out var e) && reader.TryGetByte(out var flags))
				{
					if((flags & BypassFlags) > 0)
					{
						Log.Warn($"[OnPreAuthenticating] User have bypassflags. {ev.UserId}");
						return;
					}
				}
			}

			if(plugin.Config.KickVpn)
			{
				if(ShitChecker.IsBlacklisted(ev.Request.RemoteEndPoint.Address))
				{
					writer.Reset();
					writer.Put((byte)10);
					writer.Put(Subtitles.VPNKickMessageShort);
					ev.Request.Reject(writer);
					return;
				}
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckVPN(ev)));
			}

			if(plugin.Config.KickSteamLimited && ev.UserId.Contains("@steam", StringComparison.InvariantCultureIgnoreCase))
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckIsLimitedSteam(ev.UserId)));
		}
		public void OnJoined(JoinedEventArgs ev)
		{
			Log.Info($"[OnJoined] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			if(kickedbyChecker.TryGetValue(ev.Player.UserId, out var reason))
			{
				string reasonMessage = string.Empty;
				if(reason == "steam")
					reasonMessage = Subtitles.LimitedKickMessage;
				else if(reason == "vpn")
					reasonMessage = Subtitles.VPNKickMessage;

				ServerConsole.Disconnect(ev.Player.Connection, reasonMessage);
				kickedbyChecker.Remove(ev.Player.UserId);
				return;
			}

			if(!string.IsNullOrEmpty(plugin.Config.MotdMessage))
				Methods.SendSubtitle(plugin.Config.MotdMessage.Replace("[name]", ev.Player.Nickname), 10, ev.Player);

			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled
				&& PlayerDataManager.playersData.TryGetValue(ev.Player.UserId, out PlayerData data))
				Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate);

			if(plugin.Config.DisableAllChat)
				if(!(plugin.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(ev.Player.UserId)))
					ev.Player.IsMuted = true;

			//MuteFixer
			foreach(var player in Player.List)
				if(player.IsMuted)
					player.ReferenceHub.characterClassManager.SetDirtyBit(1uL);

			//SpeedFixer
			ServerConfigSynchronizer.Singleton.SetDirtyBit(2uL);
			ServerConfigSynchronizer.Singleton.SetDirtyBit(4uL);
		}
		public void OnLeft(LeftEventArgs ev)
		{
			Log.Debug($"[OnLeft] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled && !string.IsNullOrEmpty(ev.Player.UserId))
				if(PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
					PlayerDataManager.playersData.Remove(ev.Player.UserId);
		}
		public void OnChangingRole(ChangingRoleEventArgs ev)
		{
			Log.Debug($"[OnChangingRole] {ev.Player.Nickname} -> {ev.NewRole}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.Scp079ExtendEnabled && ev.NewRole == RoleType.Scp079)
				roundCoroutines.Add(Timing.CallDelayed(10f, () => ev.Player.SendTextHint(HintTexts.Extend079First, 10)));

			if(plugin.Config.Scp106PortalExtensionEnabled && ev.NewRole == RoleType.Scp106)
				roundCoroutines.Add(Timing.CallDelayed(5f, () => ev.Player.SendTextHint(HintTexts.Extend106First, 10)));

			if(plugin.Config.DefaultitemsParsed.TryGetValue(ev.NewRole, out List<ItemType> itemconfig))
				ev.Items = itemconfig;

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						switch(ev.NewRole)
						{
							case RoleType.ClassD:
								ev.Items = plugin.Config.ClassdInsurgentInventoryClassdParsed;
								break;
							case RoleType.Scientist:
								ev.Items = plugin.Config.ClassdInsurgentInventoryScientistParsed;
								break;
						}
						break;
					}
			}

			//Fix Maingame
			ev.Player.ReferenceHub.fpc.ModifyStamina(100f);
		}
		public void OnSpawning(SpawningEventArgs ev)
		{
			Log.Debug($"[OnSpawning] {ev.Player.Nickname} -{ev.RoleType}-> {ev.Position}", SanyaPlugin.instance.Config.IsDebugged);

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.RoleType == RoleType.ClassD)
							ev.Position = LCZArmoryPos;
						break;
					}
			}
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Attacker.Role == RoleType.Spectator) return;
			Log.Debug($"[OnHurting:Before] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.instance.Config.IsDebugged);

			//GrenadeHitmark
			if(plugin.Config.HitmarkGrenade && ev.DamageType == DamageTypes.Grenade && ev.Target != ev.Attacker)
				ev.Attacker.ShowHitmarker();

			//TeslaDelete
			if(plugin.Config.TeslaDeleteItems && ev.DamageType == DamageTypes.Tesla && ev.Target.ReferenceHub.characterClassManager.IsHuman())
				ev.Target.Inventory.Clear();

			//USPMultiplier
			if(ev.DamageType == DamageTypes.Usp)
				if(ev.Target.Team == Team.SCP)
					ev.Amount *= plugin.Config.UspDamageMultiplierScp;
				else
					ev.Amount *= plugin.Config.UspDamageMultiplierHuman;

			//SCP-939 Bleeding
			if(plugin.Config.Scp939AttackBleeding && ev.DamageType == DamageTypes.Scp939)
			{
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Bleeding>();
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Hemorrhage>();
			}

			//SCP-049-2 Effect
			if(plugin.Config.Scp0492AttackEffect && ev.DamageType == DamageTypes.Scp0492)
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Blinded>(2f);

			//SCP-173 Force Blink
			if(plugin.Config.Scp173ForceBlinkPercent > 0 && ev.Target.Role == RoleType.Scp173 && plugin.Random.Next(0, 100) < plugin.Config.Scp173ForceBlinkPercent)
				Methods.Blink();


			//CuffedMultiplier
			if(ev.Target.IsCuffed)
				ev.Amount *= plugin.Config.CuffedDamageMultiplier;

			//SCPsMultiplier
			if(ev.Attacker != ev.Target)
				switch(ev.Target.Role)
				{
					case RoleType.Scp173:
						ev.Amount *= plugin.Config.Scp173DamageMultiplier;
						break;
					case RoleType.Scp106:
						if(ev.DamageType == DamageTypes.Grenade)
							ev.Amount *= plugin.Config.Scp106GrenadeMultiplier;
						else
							ev.Amount *= plugin.Config.Scp106DamageMultiplier;
						break;
					case RoleType.Scp049:
						ev.Amount *= plugin.Config.Scp049DamageMultiplier;
						break;
					case RoleType.Scp096:
						ev.Amount *= plugin.Config.Scp096DamageMultiplier;
						break;
					case RoleType.Scp0492:
						ev.Amount *= plugin.Config.Scp0492DamageMultiplier;
						break;
					case RoleType.Scp93953:
					case RoleType.Scp93989:
						ev.Amount *= plugin.Config.Scp939DamageMultiplier;
						break;
				}

			Log.Debug($"[OnHurting:After] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.instance.Config.IsDebugged);
		}
		public void OnDied(DiedEventArgs ev)
		{
			if(ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.Spectator) return;
			Log.Debug($"[OnDied] {ev.Killer.Nickname}[{ev.Killer.Role}] -{ev.HitInformations.GetDamageName()}-> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.instance.Config.IsDebugged);
			var targetteam = ev.Target.ReferenceHub.characterClassManager._prevId.GetTeam();
			var targetrole = ev.Target.ReferenceHub.characterClassManager._prevId;

			if(plugin.Config.DataEnabled)
			{
				if(!string.IsNullOrEmpty(ev.Killer.UserId) && ev.Killer != ev.Target && PlayerDataManager.playersData.ContainsKey(ev.Killer.UserId))
					PlayerDataManager.playersData[ev.Killer.UserId].AddExp(plugin.Config.LevelExpKill);

				if(PlayerDataManager.playersData.ContainsKey(ev.Target.UserId))
					PlayerDataManager.playersData[ev.Target.UserId].AddExp(plugin.Config.LevelExpDeath);
			}

			//HitmarkKilled
			if(plugin.Config.HitmarkKilled && ev.Killer != ev.Target)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmark(ev.Killer.GameObject.GetComponent<MicroHID>())));

			//SCPsRecovery
			switch(ev.Killer.Role)
			{
				case RoleType.Scp173:
					ev.Killer.Health += plugin.Config.Scp173RecoveryAmount;
					break;
				case RoleType.Scp106:
					ev.Killer.Health += plugin.Config.Scp106RecoveryAmount;
					break;
				case RoleType.Scp096:
					ev.Killer.Health += plugin.Config.Scp096RecoveryAmount;
					break;
				case RoleType.Scp0492:
					ev.Killer.Health += plugin.Config.Scp0492RecoveryAmount;
					break;
				case RoleType.Scp93953:
				case RoleType.Scp93989:
					ev.Killer.Health += plugin.Config.Scp939RecoveryAmount;
					break;
			}

			//SCP-939 RemoveItems
			if(plugin.Config.Scp939RemoveItem && ev.HitInformations.GetDamageType() == DamageTypes.Scp939)
				ev.Target.Inventory.Clear();

			//Ticket Extend
			switch(targetteam)
			{
				case Team.CDP:
					if(ev.Killer.Team != Team.SCP) Respawning.RespawnTickets.Singleton.GrantTickets(Respawning.SpawnableTeamType.ChaosInsurgency, plugin.Config.TicketsCiClassdDiedCount);
					if(ev.Killer.Team == Team.MTF || ev.Killer.Team == Team.RSC) Respawning.RespawnTickets.Singleton.GrantTickets(Respawning.SpawnableTeamType.NineTailedFox, plugin.Config.TicketsMtfClassdDiedCount);
					break;
				case Team.RSC:
					Respawning.RespawnTickets.Singleton.GrantTickets(Respawning.SpawnableTeamType.NineTailedFox, plugin.Config.TicketsMtfScientistDiedCount);
					break;
				case Team.MTF:
					if(ev.Killer.Team == Team.SCP) Respawning.RespawnTickets.Singleton.GrantTickets(Respawning.SpawnableTeamType.NineTailedFox, plugin.Config.TicketsMtfKilledByScpCount);
					break;
				case Team.CHI:
					if(ev.Killer.Team == Team.SCP) Respawning.RespawnTickets.Singleton.GrantTickets(Respawning.SpawnableTeamType.ChaosInsurgency, plugin.Config.TicketsCiKilledByScpCount);
					break;
			}

			//CassieSubtitle
			if(plugin.Config.CassieSubtitle && targetteam == Team.SCP && targetrole != RoleType.Scp0492 && targetrole != RoleType.Scp079)
			{
				var damageTypes = ev.HitInformations.GetDamageType();
				string fullname = CharacterClassManager._staticClasses.Get(targetrole).fullName;
				string str;

				if(damageTypes == DamageTypes.Tesla)
					str = Subtitles.SCPDeathTesla.Replace("{0}", fullname);
				else if(damageTypes == DamageTypes.Nuke)
					str = Subtitles.SCPDeathWarhead.Replace("{0}", fullname);
				else if(damageTypes == DamageTypes.Decont)
					str = Subtitles.SCPDeathDecont.Replace("{0}", fullname);
				else
				{
					if(ev.Killer.Team == Team.CDP)
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "Dクラス職員").Replace("{2}", "Class-D Personnel");
					else if(ev.Killer.Team == Team.CHI)
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "カオス・インサージェンシー").Replace("{2}", "Chaos Insurgency");
					else if(ev.Killer.Team == Team.RSC)
						str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "研究員").Replace("{2}", "Science Personnel");
					else if(ev.Killer.Team == Team.MTF)
						str = Subtitles.SCPDeathContainedMTF.Replace("{0}", fullname).Replace("{1}", ev.Killer.ReferenceHub.characterClassManager.CurUnitName);
					else
						str = Subtitles.SCPDeathUnknown.Replace("{0}", fullname);
				}

				if(Player.List.Any(x => x.Role == RoleType.Scp079) && Player.List.Count(x => x.Team == Team.SCP) == 1
					&& Generator079.mainGenerator.totalVoltage < 4 && !Generator079.mainGenerator.forcedOvercharge && damageTypes != DamageTypes.Nuke)
					str = str
						.Replace("{-1}", "\n全てのSCPオブジェクトの安全が確保されました。SCP-079の再収用手順を開始します。\n重度収用区画は約一分後にオーバーチャージされます。")
						.Replace("{-2}", "\nAll SCP subject has been secured. SCP-079 recontainment sequence commencing.\nHeavy containment zone will overcharge in t-minus 1 minutes.");
				else
					str = str
						.Replace("{-1}", string.Empty)
						.Replace("{-2}", string.Empty);

				Methods.SendSubtitle(str, (ushort)(str.Contains("t-minus") ? 30 : 10));
			}
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled)
				foreach(var player in Player.List)
					if(player.Role == RoleType.Scp106 && PlayerDataManager.playersData.ContainsKey(player.UserId))
						PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpKill);

			foreach(var player in Player.List.Where(x => x.Role == RoleType.Scp106))
			{
				player.Health += plugin.Config.Scp106RecoveryAmount;
				player.ShowHitmarker();
			}
		}
		public void OnSyncingData(SyncingDataEventArgs ev)
		{
			if(ev.Player == null || ev.Player.IsHost || !ev.Player.ReferenceHub.isReady || ev.Player.ReferenceHub.animationController.curAnim == ev.CurrentAnimation) return;

			if(plugin.Config.Scp079ExtendEnabled && ev.Player.Role == RoleType.Scp079
				|| plugin.Config.Scp106PortalExtensionEnabled && ev.Player.Role == RoleType.Scp106 && ev.Player.ReferenceHub.animationController.curAnim != 2 && ev.CurrentAnimation != 2)
				if(ev.CurrentAnimation == 1)
					ev.Player.SendTextHint(HintTexts.ExtendEnabled, 5);
				else
					ev.Player.SendTextHint(HintTexts.ExtendDisabled, 5);

			if(plugin.Config.StaminaLostJump > 0 && ev.CurrentAnimation == 2 && ev.Player.ReferenceHub.characterClassManager.IsHuman()
				&& !ev.Player.ReferenceHub.fpc.staminaController._invigorated.Enabled && !ev.Player.ReferenceHub.fpc.staminaController._scp207.Enabled)
			{
				ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina -= plugin.Config.StaminaLostJump;
				ev.Player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;

				if(ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina <= 0f)
				{
					ev.Player.ReferenceHub.playerEffectsController.EnableEffect<Disabled>(7f);
					ev.Player.ReferenceHub.playerEffectsController.EnableEffect<Concussed>(5f);
				}
			}
		}
		public void OnUsedMedicalItem(UsedMedicalItemEventArgs ev)
		{
			Log.Debug($"[OnUsedMedicalItem] {ev.Player.Nickname} -> {ev.Item}", SanyaPlugin.instance.Config.IsDebugged);

			if(ev.Item == (ItemType.Medkit | ItemType.SCP500))
			{
				ev.Player.ReferenceHub.playerEffectsController.DisableEffect<Hemorrhage>();
				ev.Player.ReferenceHub.playerEffectsController.DisableEffect<Bleeding>();
			}
		}
		public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
		{
			if(plugin.Config.TeslaTriggerableTeams.Count == 0 || plugin.Config.TeslaTriggerableTeamsParsed.Contains(ev.Player.Team))
			{
				if(plugin.Config.TeslaTriggerableDisarmed || !ev.Player.IsCuffed)
					ev.IsTriggerable = true;
				else
					ev.IsTriggerable = false;
			}
			else
				ev.IsTriggerable = false;
		}
		public void OnInteractingDoor(InteractingDoorEventArgs ev)
		{
			Log.Debug($"[OnInteractingDoor] {ev.Player.Nickname}:{ev.Door.DoorName}:{ev.Door.PermissionLevels}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.InventoryKeycardActivation && ev.Player.Team != Team.SCP && !ev.Player.IsBypassModeEnabled && !ev.Door.locked)
				foreach(var item in ev.Player.Inventory.items)
					foreach(var permission in ev.Player.Inventory.GetItemByID(item.id).permissions)
						if(ev.Door.backwardsCompatPermissions.TryGetValue(permission, out var flag) && ev.Door.PermissionLevels.HasPermission(flag))
							ev.IsAllowed = true;
		}
		public void OnInteractingLocker(InteractingLockerEventArgs ev)
		{
			Log.Debug($"[OnInteractingLocker] {ev.Player.Nickname}:{ev.Id}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.InventoryKeycardActivation)
				foreach(var item in ev.Player.Inventory.items)
					if(ev.Player.Inventory.GetItemByID(item.id).permissions.Contains("PEDESTAL_ACC"))
						ev.IsAllowed = true;
		}
		public void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
		{
			Log.Debug($"[OnUnlockingGenerator] {ev.Player.Nickname} -> {ev.Generator.CurRoom}", SanyaPlugin.instance.Config.IsDebugged);
			if(plugin.Config.InventoryKeycardActivation && !ev.Player.IsBypassModeEnabled)
				foreach(var item in ev.Player.Inventory.items)
					if(ev.Player.Inventory.GetItemByID(item.id).permissions.Contains("ARMORY_LVL_2"))
						ev.IsAllowed = true;

			if(ev.IsAllowed && plugin.Config.GeneratorUnlockOpen)
			{
				ev.Generator._doorAnimationCooldown = 2f;
				ev.Generator.NetworkisDoorOpen = true;
				ev.Generator.RpcDoSound(true);
			}
		}
		public void OnOpeningGenerator(OpeningGeneratorEventArgs ev)
		{
			Log.Debug($"[OnOpeningGenerator] {ev.Player.Nickname} -> {ev.Generator.CurRoom}", SanyaPlugin.instance.Config.IsDebugged);

			if(ev.Generator.prevFinish && plugin.Config.GeneratorFinishLock)
				ev.IsAllowed = false;
		}

		//Scp049
		public void OnFinishingRecall(FinishingRecallEventArgs ev)
		{
			Log.Debug($"[OnFinishingRecall] {ev.Scp049.Nickname} -> {ev.Target.Nickname}", SanyaPlugin.instance.Config.IsDebugged);

			ev.Scp049.Health += plugin.Config.Scp049RecoveryAmount;

			if(plugin.Config.Scp049ExtensionRecallTime)
				foreach(var target in Player.List)
					target.AddDeathTimeForScp049();
		}

		//Scp079
		public void OnGainingLevel(GainingLevelEventArgs ev)
		{
			Log.Debug($"[OnGainingLevel] {ev.Player.Nickname} {ev.OldLevel} -> {ev.NewLevel}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.Scp079ExtendEnabled)
				switch(ev.OldLevel)
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

		//Scp106
		public void OnCreatingPortal(CreatingPortalEventArgs ev)
		{
			Log.Debug($"[OnCreatingPortal] {ev.Player.Nickname}:{ev.Position}:{ev.Player.IsExmode()}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.Scp106PortalExtensionEnabled && ev.Player.Role == RoleType.Scp106)
			{
				var scp106 = ev.Player.GameObject.GetComponent<Scp106PlayerScript>();
				if(!scp106.goingViaThePortal && ev.Player.ReferenceHub.falldamage.isGrounded && ev.Player.IsExmode())
				{
					var target = Player.List.Where(
						x => x.Team != Team.SCP
						&& x.Team != Team.RIP
						&& x.Team != Team.TUT
						&& x.ReferenceHub.falldamage.isGrounded
						&& !x.ReferenceHub.playerEffectsController.GetEffect<Corroding>().Enabled)
					.Random();

					if(target == null)
					{
						ev.Player.SendTextHint(HintTexts.Extend106TargetNotFound, 5);
						ev.IsAllowed = false;
					}
					else
						if(Physics.Raycast(new Ray(target.Position, -target.GameObject.transform.up), out RaycastHit raycastHit, 10f, scp106.teleportPlacementMask))
					{
						ev.Player.SendTextHint(HintTexts.Extend106Success, 5);
						ev.Position = raycastHit.point - Vector3.up;
					}
				}
			}
		}

		//Scp914
		public void OnUpgradingItems(UpgradingItemsEventArgs ev)
		{
			Log.Debug($"[OnUpgradingItems] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}", SanyaPlugin.instance.Config.IsDebugged);

			if(plugin.Config.Scp914IntakeDeath)
				foreach(var player in ev.Players)
				{
					player.Inventory.Clear();
					player.ReferenceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0), player.GameObject);
				}
		}
	}
}