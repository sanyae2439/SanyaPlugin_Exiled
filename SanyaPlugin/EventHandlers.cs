using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CustomPlayerEffects;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events;
using Exiled.Events.EventArgs;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MEC;
using Mirror;
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
		public readonly SanyaPlugin plugin;
		public List<CoroutineHandle> roundCoroutines = new List<CoroutineHandle>();
		private bool loaded = false;

		/** Infosender **/
		private readonly UdpClient udpClient = new UdpClient();
		internal Task sendertask;
		internal async Task SenderAsync()
		{
			Log.Debug($"[SenderAsync] Started.", SanyaPlugin.Instance.Config.IsDebugged);

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
						Log.Debug($"[SenderAsync] Plugin not loaded. Skipped...", SanyaPlugin.Instance.Config.IsDebugged);
						await Task.Delay(TimeSpan.FromSeconds(30));
					}

					Serverinfo cinfo = new Serverinfo();

					DateTime dt = DateTime.Now;
					cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
					cinfo.gameversion = $"{GameCore.Version.Major}.{GameCore.Version.Minor}.{GameCore.Version.Revision}";
					cinfo.modversion = $"{Events.Instance.Version.Major}.{Events.Instance.Version.Minor}.{Events.Instance.Version.Build}";
					cinfo.sanyaversion = SanyaPlugin.Instance.Version.ToString();
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
					Log.Debug($"[SenderAsync] {plugin.Config.InfosenderIp}:{plugin.Config.InfosenderPort}", SanyaPlugin.Instance.Config.IsDebugged);
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

					//ItemCleanup
					if(plugin.Config.ItemCleanup > 0)
					{
						List<GameObject> nowitems = null;

						foreach(var i in ItemCleanupPatch.items)
						{
							if(Time.time - i.Value > plugin.Config.ItemCleanup && i.Key != null)
							{
								if(nowitems == null) nowitems = new List<GameObject>();
								Log.Debug($"[ItemCleanup] Cleanup:{i.Key.transform.position} {Time.time - i.Value} > {plugin.Config.ItemCleanup}", SanyaPlugin.Instance.Config.IsDebugged);
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

					//停電時強制再収容の際復電
					if(eventmode == SANYA_GAME_MODE.NIGHT && IsEnableBlackout && Generator079.mainGenerator.forcedOvercharge)
					{
						IsEnableBlackout = false;
					}

					//SCP-079's Spot Humans
					if(plugin.Config.Scp079ExtendEnabled && plugin.Config.Scp079ExtendLevelSpot > 0)
					{
						List<Player> foundplayers = new List<Player>();
						var scp079 = Scp079PlayerScript.instances.Count != 0 ? Player.Get(Scp079PlayerScript.instances.First().gameObject) : null;
						string message = string.Empty;
						if(scp079 != null && scp079.IsExmode() && last079cam != scp079.Camera)
						{
							bool generatordetected = false;
							var generator = Generator079.Generators.Find(x => x.CurRoom.ToLower() == scp079.CurrentRoom.Name.ToLower());
							if(generator != null && generator.isTabletConnected && !generator.prevFinish)
							{
								generatordetected = true;
								message = $"<color=#bbee00><size=25>発電機が起動を開始している\n場所：{scp079.CurrentRoom.Type}</color></size>\n";
							}

							if(!generatordetected)
							{
								foreach(var player in Player.List.Where(x => x.Team != Team.RIP && x.Team != Team.SCP))
								{
									if(player.ReferenceHub.characterClassManager.IsHuman() && scp079.CurrentRoom != null && scp079.CurrentRoom.Players.Contains(player))
									{
										last079cam = scp079.Camera;
										foundplayers.Add(player);
										message = $"<color=#bbee00><size=25>SCP-079が{player.ReferenceHub.characterClassManager.CurRole.fullName}を発見した\n場所：{player.CurrentRoom.Type}</color></size>\n";
										break;
									}
								}
							}
						}

						if(!string.IsNullOrEmpty(message))
						{
							foreach(var scp in Player.Get(Team.SCP))
							{
								scp.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(message, 5);
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
		private IEnumerator<float> FixedUpdate()
		{
			while(true)
			{
				try
				{
					//Blackouter
					if(flickerableLightController != null && IsEnableBlackout && !flickerableLightController.IsEnabled())
					{
						Log.Debug($"[Blackouter] Fired.", SanyaPlugin.Instance.Config.IsDebugged);
						Generator079.mainGenerator.ServerOvercharge(10f, false);
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

		/** RoundVar **/
		public readonly static Dictionary<string, uint> DamagesDict = new Dictionary<string, uint>();
		public readonly static Dictionary<string, uint> KillsDict = new Dictionary<string, uint>();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		private FlickerableLightController flickerableLightController = null;
		internal bool IsEnableBlackout = false;
		private Vector3 nextRespawnPos = Vector3.zero;
		private Camera079 last079cam = null;
		internal int scp049stackAmount = 0;
		internal Player Overrided = null;
		public bool FriendlyFlashEnabled = false;
		internal NetworkIdentity Sinkhole = null;

		/** EventModeVar **/
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		private Room lczarmony = null;

		//ServerEvents
		public void OnWaintingForPlayers()
		{
			loaded = true;

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

			flickerableLightController = UnityEngine.Object.FindObjectOfType<FlickerableLightController>();

			last079cam = null;
			scp049stackAmount = 0;

			foreach(var i in plugin.Config.RemoveScp914RecipeParsed)
				Methods.Remove914Item(i);
			Methods.Add914RecipeCoin();
			FriendlyFlashEnabled = GameCore.ConfigFile.ServerConfig.GetBool("friendly_flash", false);
			Sinkhole = Methods.GetSinkHoleHazard();
			if(Sinkhole != null) Methods.MoveNetworkIdentityObject(Sinkhole, Map.GetRandomSpawnPoint(RoleType.Scp106) - (-Vector3.down * 4));

			if(plugin.Config.AddDoorsOnSurface)
			{
				var LCZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("LCZ"));
				var EZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("EZ"));
				var HCZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("HCZ"));

				var door1 = UnityEngine.Object.Instantiate(LCZprefab.TargetPrefab, new UnityEngine.Vector3(14.425f, 995.2f, -43.525f), Quaternion.Euler(Vector3.zero));
				(door1 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				var door2 = UnityEngine.Object.Instantiate(LCZprefab.TargetPrefab, new UnityEngine.Vector3(14.425f, 995.2f, -23.25f), Quaternion.Euler(Vector3.zero));
				(door2 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				var door3 = UnityEngine.Object.Instantiate(EZprefab.TargetPrefab, new UnityEngine.Vector3(176.2f, 983.24f, 35.23f), Quaternion.Euler(Vector3.up * 180f));
				(door3 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				var door4 = UnityEngine.Object.Instantiate(EZprefab.TargetPrefab, new UnityEngine.Vector3(174.4f, 983.24f, 29.1f), Quaternion.Euler(Vector3.up * 90f));
				(door4 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

				var door5 = UnityEngine.Object.Instantiate(HCZprefab.TargetPrefab, new UnityEngine.Vector3(1.15f, 1000f, 4.8f), Quaternion.Euler(Vector3.zero));
				(door5 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				var door6 = UnityEngine.Object.Instantiate(HCZprefab.TargetPrefab, new UnityEngine.Vector3(-1.27f, 1000f, 4.8f), Quaternion.Euler(Vector3.zero));
				(door6 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

				door5.gameObject.AddComponent<DoorNametagExtension>().UpdateName("GATE_EX_R");
				door6.gameObject.AddComponent<DoorNametagExtension>().UpdateName("GATE_EX_L");

				NetworkServer.Spawn(door1.gameObject);
				NetworkServer.Spawn(door2.gameObject);
				NetworkServer.Spawn(door3.gameObject);
				NetworkServer.Spawn(door4.gameObject);
				NetworkServer.Spawn(door5.gameObject);
				NetworkServer.Spawn(door6.gameObject);
			}

			(DoorNametagExtension.NamedDoors.First(x => x.Key == "SURFACE_NUKE").Value.TargetDoor as BreakableDoor)._ignoredDamageSources &= ~DoorDamageType.Grenade;

			if(plugin.Config.WarheadInitCountdown > 0)
			{
				int realtime = Mathf.RoundToInt(Mathf.Clamp(plugin.Config.WarheadInitCountdown, 30, 120) / 10f) * 10;

				if(realtime >= 80)
				{
					byte index = (byte)AlphaWarheadController.Host.scenarios_start.ToList().FindIndex(x => x.tMinusTime == realtime);
					AlphaWarheadController.Host.NetworksyncStartScenario = index;
					AlphaWarheadController.Host.NetworktimeToDetonation = AlphaWarheadController.Host.scenarios_start[index].tMinusTime + AlphaWarheadController.Host.scenarios_start[index].additionalTime;
				}
				else if(realtime < 80)
				{
					sbyte index = (sbyte)AlphaWarheadController.Host.scenarios_resume.ToList().FindIndex(x => x.tMinusTime == realtime);
					AlphaWarheadController.Host.NetworksyncResumeScenario = index;
					AlphaWarheadController.Host.NetworktimeToDetonation = AlphaWarheadController.Host.scenarios_resume[index].tMinusTime + AlphaWarheadController.Host.scenarios_resume[index].additionalTime;
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
						lczarmony = Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.LczArmory);
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
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.StartNightMode()));
						break;
					}
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.ClassDInsurgencyInit()));
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

			sortedDamages = DamagesDict.OrderByDescending(x => x.Value);
			sortedKills = KillsDict.OrderByDescending(x => x.Value);
		}
		public void OnRestartingRound()
		{
			Log.Info($"[OnRestartingRound] Restarting...");

			foreach(var player in Player.List)
				if(player.GameObject.TryGetComponent<SanyaPluginComponent>(out var comp))
					UnityEngine.Object.Destroy(comp);

			foreach(var cor in roundCoroutines)
				Timing.KillCoroutines(cor);
			roundCoroutines.Clear();

			RoundSummary.singleton._roundEnded = true;
			sortedDamages = null;
			DamagesDict.Clear();
			sortedKills = null;
			KillsDict.Clear();
			SanyaPluginComponent.scplists.Clear();
		}
		public void OnReloadConfigs()
		{
			Log.Debug($"[OnReloadConfigs]", SanyaPlugin.Instance.Config.IsDebugged);

			plugin.Config.ParseConfig();
		}
		public void OnRespawningTeam(RespawningTeamEventArgs ev)
		{
			Log.Debug($"[OnRespawningTeam] Queues:{ev.Players.Count} IsCI:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.StopRespawnAfterDetonated && Warhead.IsDetonated || plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
				ev.Players.Clear();

			if(plugin.Config.RandomRespawnPosPercent > 0)
			{
				int randomnum = UnityEngine.Random.Range(0, 100);
				Log.Debug($"[RandomRespawnPos] Check:{randomnum}>{plugin.Config.RandomRespawnPosPercent}", SanyaPlugin.Instance.Config.IsDebugged);
				if(randomnum > plugin.Config.RandomRespawnPosPercent && !Warhead.IsDetonated && !Warhead.IsInProgress)
				{
					List<Vector3> poslist = new List<Vector3>();
					poslist.Add(RoleType.Scp049.GetRandomSpawnPoint());
					poslist.Add(RoleType.Scp93953.GetRandomSpawnPoint());

					if(!Map.IsLCZDecontaminated && DecontaminationController.Singleton._nextPhase < 3)
					{
						poslist.Add(Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.LczArmory).Position);

						foreach(var itempos in RandomItemSpawner.singleton.posIds)
						{
							if(itempos.posID == "RandomPistol" && itempos.position.position.y > 0.5f && itempos.position.position.y < 0.7f)
							{
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							}
							else if(itempos.posID == "toilet_keycard" && itempos.position.position.y > 1.25f && itempos.position.position.y < 1.35f)
							{
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y - 0.5f, itempos.position.position.z));
							}
						}
					}

					foreach(GameObject roomid in GameObject.FindGameObjectsWithTag("RoomID"))
					{
						Rid rid = roomid.GetComponent<Rid>();
						if(rid != null && (rid.id == "LC_ARMORY" || rid.id == "Shelter" || rid.id == "nukesite"))
						{
							poslist.Add(roomid.transform.position);
						}
					}

					foreach(var i in poslist)
					{
						Log.Debug($"[RandomRespawnPos] TargetLists:{i}", SanyaPlugin.Instance.Config.IsDebugged);
					}

					int randomnumlast = UnityEngine.Random.Range(0, poslist.Count);
					nextRespawnPos = new Vector3(poslist[randomnumlast].x, poslist[randomnumlast].y + 2, poslist[randomnumlast].z);

					Log.Info($"[RandomRespawnPos] Determined:{nextRespawnPos}");
				}
				else
				{
					nextRespawnPos = Vector3.zero;
				}
			}
		}

		//MapEvents
		public void OnAnnouncingDecontamination(AnnouncingDecontaminationEventArgs ev)
		{
			Log.Debug($"[OnAnnouncingDecontamination] {ev.Id}", SanyaPlugin.Instance.Config.IsDebugged);

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
			Log.Debug($"[OnDecontaminating]", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.DecontaminationLockdown, 15);
		}
		public void OnGeneratorActivated(GeneratorActivatedEventArgs ev)
		{
			Log.Debug($"[OnGeneratorActivated] {ev.Generator.CurRoom}", SanyaPlugin.Instance.Config.IsDebugged);
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
			Log.Debug($"[OnStarting] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

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
			Log.Debug($"[OnStopping] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			if(AlphaWarheadController.Host._isLocked) return;

			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
		}
		public void OnDetonated()
		{
			Log.Info($"[OnDetonated] Detonated:{RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}");

			detonatedDuration = RoundSummary.roundTime;
		}

		//PlayerEvents
		public void OnPreAuthenticating(PreAuthenticatingEventArgs ev)
		{
			Log.Debug($"[OnPreAuthenticating] {ev.Request.RemoteEndPoint.Address}:{ev.UserId}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled && !PlayerDataManager.playersData.ContainsKey(ev.UserId))
				PlayerDataManager.playersData.Add(ev.UserId, PlayerDataManager.LoadPlayerData(ev.UserId));

			if(ev.UserId.Contains("@northwood") || (ev.Flags & BypassFlags) > 0)
			{
				Log.Warn($"[OnPreAuthenticating] User have bypassflags. {ev.UserId}");
				return;
			}

			if(!string.IsNullOrEmpty(plugin.Config.KickVpnApikey))
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

			if(plugin.Config.KickSteamLimited && ev.UserId.Contains("@steam"))
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckIsLimitedSteam(ev.UserId)));
		}
		public void OnVerified(VerifiedEventArgs ev)
		{
			Log.Info($"[OnVerified] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			if(plugin.Config.DataEnabled && ev.Player.DoNotTrack && PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
				PlayerDataManager.playersData.Remove(ev.Player.UserId);

			if(plugin.Config.DataEnabled && !ev.Player.DoNotTrack && !PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
				PlayerDataManager.playersData.Add(ev.Player.UserId, PlayerDataManager.LoadPlayerData(ev.Player.UserId));

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

			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled
				&& PlayerDataManager.playersData.TryGetValue(ev.Player.UserId, out PlayerData data))
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data)));

			if(plugin.Config.DisableAllChat)
				if(!(plugin.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(ev.Player.UserId)))
					ev.Player.IsMuted = true;

			if(plugin.Config.WaitingTutorials && !ReferenceHub.HostHub.characterClassManager.RoundStarted)
			{
				NetworkIdentity identitytarget = null;
				foreach(var identity in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
					if(identity.name == "StartRound")
						identitytarget = identity;

				identitytarget.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
				ObjectDestroyMessage objectDestroyMessage = new ObjectDestroyMessage();
				objectDestroyMessage.netId = identitytarget.netId;
				ev.Player.Connection.Send(objectDestroyMessage, 0);
				typeof(NetworkServer).GetMethod("SendSpawnMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { identitytarget, ev.Player.Connection });

				roundCoroutines.Add(Timing.CallDelayed(0.25f, () => { ev.Player.ReferenceHub.characterClassManager.SetPlayersClass(RoleType.Tutorial, ev.Player.GameObject); }));
			}

			if(!string.IsNullOrEmpty(plugin.Config.MotdMessageOnDisabledChat) && plugin.Config.DisableChatBypassWhitelist && !WhiteList.IsOnWhitelist(ev.Player.UserId) && ev.Player.IsMuted)
				Methods.SendSubtitle(plugin.Config.MotdMessageOnDisabledChat.Replace("[name]", ev.Player.Nickname), 10, ev.Player);
			else if(!string.IsNullOrEmpty(plugin.Config.MotdMessage))
				Methods.SendSubtitle(plugin.Config.MotdMessage.Replace("[name]", ev.Player.Nickname), 10, ev.Player);

			if(plugin.Config.PlayersInfoDisableFollow)
				ev.Player.ReferenceHub.nicknameSync.Network_playerInfoToShow = PlayerInfoArea.Nickname | PlayerInfoArea.Badge | PlayerInfoArea.CustomInfo | PlayerInfoArea.Role;

			//MuteFixer
			foreach(var player in Player.List)
				if(player.IsMuted)
					player.ReferenceHub.characterClassManager.SetDirtyBit(2uL);

			//Component
			if(!ev.Player.GameObject.TryGetComponent<SanyaPluginComponent>(out _))
				ev.Player.GameObject.AddComponent<SanyaPluginComponent>();

			//DamageDict
			if(!DamagesDict.TryGetValue(ev.Player.Nickname, out _))
				DamagesDict.Add(ev.Player.Nickname, 0);

			//KillDict
			if(!KillsDict.TryGetValue(ev.Player.Nickname, out _))
				KillsDict.Add(ev.Player.Nickname, 0);
		}
		public void OnDestroying(DestroyingEventArgs ev)
		{
			Log.Debug($"[OnDestroying] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled && !string.IsNullOrEmpty(ev.Player.UserId))
				if(PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
					PlayerDataManager.playersData.Remove(ev.Player.UserId);
		}
		public void OnChangingRole(ChangingRoleEventArgs ev)
		{
			if(ev.Player.Nickname == null) return;
			Log.Debug($"[OnChangingRole] {ev.Player.Nickname} [{ev.Player.ReferenceHub.characterClassManager._prevId}] -> [{ev.NewRole}] ({ev.IsEscaped})", SanyaPlugin.Instance.Config.IsDebugged);

			if(Overrided != null && Overrided == ev.Player)
			{
				if(ev.NewRole.GetTeam() != Team.SCP)
				{
					ev.NewRole = (RoleType)ReferenceHub.HostHub.characterClassManager.FindRandomIdUsingDefinedTeam(Team.SCP);
					RoundSummary.singleton.classlistStart.scps_except_zombies++;
				}
				Overrided = null;
			}

			if(plugin.Config.ExHudEnabled && plugin.Config.Scp079ExtendEnabled && ev.NewRole == RoleType.Scp079)
				roundCoroutines.Add(Timing.CallDelayed(10f, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079First, 10)));

			if(plugin.Config.ExHudEnabled && plugin.Config.Scp049StackBody && ev.NewRole == RoleType.Scp049)
				roundCoroutines.Add(Timing.CallDelayed(3f, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend049First, 10)));

			if(plugin.Config.ExHudEnabled && plugin.Config.Scp106Exmode && ev.NewRole == RoleType.Scp106)
				roundCoroutines.Add(Timing.CallDelayed(3f, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend106First, 10)));

			if(plugin.Config.DefaultitemsParsed.TryGetValue(ev.NewRole, out List<ItemType> itemconfig))
			{
				if(itemconfig.Contains(ItemType.None)) ev.Items.Clear();
				else
				{
					ev.Items.Clear();
					ev.Items.AddRange(itemconfig);
				}
			}

			if(plugin.Config.DefaultitemsEscapeClassd.Count > 0 && ev.NewRole == RoleType.ChaosInsurgency && ev.IsEscaped)
			{
				ev.Items.Clear();
				ev.Items.AddRange(plugin.Config.DefaultitemsEscapeClassdParsed);
			}

			//Fix Maingame
			ev.Player.ReferenceHub.fpc.ModifyStamina(100f);

			//ScpAhp
			if(ev.NewRole.GetTeam() != Team.SCP)
			{
				ev.Player.ReferenceHub.playerStats.NetworkmaxArtificialHealth = 75;
				ev.Player.ReferenceHub.playerStats.NetworkartificialHpDecay = 0.75f;
				ev.Player.ReferenceHub.playerStats.NetworkartificialNormalRatio = 0.7f;
			}
			else if(ev.NewRole.Is939() || ev.NewRole == RoleType.Scp173 || ev.NewRole == RoleType.Scp049)
			{
				ev.Player.ReferenceHub.playerStats.NetworkmaxArtificialHealth = 0;
				ev.Player.ReferenceHub.playerStats.NetworkartificialHpDecay = 0f;
				ev.Player.ReferenceHub.playerStats.NetworkartificialNormalRatio = 1f;
			}
			else if(ev.NewRole == RoleType.Scp106)
			{
				ev.Player.ReferenceHub.playerStats.NetworkmaxArtificialHealth = 0;
				ev.Player.ReferenceHub.playerStats.NetworkartificialHpDecay = -0.25f;
				ev.Player.ReferenceHub.playerStats.NetworkartificialNormalRatio = 1f;
			}

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.NewRole == RoleType.ClassD)
						{
							if(plugin.Config.DefaultitemsParsed.TryGetValue(RoleType.ChaosInsurgency, out List<ItemType> classDInsurgencyitems))
							{
								if(classDInsurgencyitems.Contains(ItemType.None)) ev.Items.Clear();
								else
								{
									ev.Items.Clear();
									ev.Items.AddRange(classDInsurgencyitems);
								}
							}
						}
						break;
					}

			}
		}
		public void OnSpawning(SpawningEventArgs ev)
		{
			Log.Debug($"[OnSpawning] {ev.Player.Nickname}(old:{ev.Player.ReferenceHub.characterClassManager._prevId}) -{ev.RoleType}-> {ev.Position}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.RandomRespawnPosPercent > 0
				&& ev.Player.ReferenceHub.characterClassManager._prevId == RoleType.Spectator
				&& (ev.RoleType.GetTeam() == Team.MTF || ev.RoleType.GetTeam() == Team.CHI)
				&& nextRespawnPos != Vector3.zero)
				ev.Position = nextRespawnPos;

			if(plugin.Config.ScientistsChangeSpawnPos && ev.RoleType == RoleType.Scientist)
			{
				ev.Position = Map.GetRandomSpawnPoint(RoleType.FacilityGuard);
			}

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.RoleType == RoleType.ClassD)
						{
							ev.Position = lczarmony.Position + Vector3.up;
							ev.Player.Ammo.amount.Clear();
							foreach(var ammo in ev.Player.ReferenceHub.characterClassManager.Classes.SafeGet(RoleType.ChaosInsurgency).ammoTypes)
								ev.Player.Ammo.amount.Add(ammo);
						}
						break;
					}
			}

			//EXILED fix
			ev.Player.ReferenceHub.playerMovementSync.IsAFK = true;
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Attacker.Role == RoleType.Spectator || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnHurting:Before] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//GrenadeHitmark
			if(plugin.Config.HitmarkGrenade && ev.DamageType == DamageTypes.Grenade && ev.Target != ev.Attacker)
				ev.Attacker.ShowHitmarker();

			//TeslaDelete
			if(plugin.Config.TeslaDeleteObjects && ev.DamageType == DamageTypes.Tesla && ev.Target.ReferenceHub.characterClassManager.IsHuman())
				ev.Target.Inventory.Clear();

			//USPMultiplier
			if(ev.DamageType == DamageTypes.Usp)
				if(ev.Target.Team == Team.SCP)
					ev.Amount *= plugin.Config.UspDamageMultiplierScp;
				else
					ev.Amount *= plugin.Config.UspDamageMultiplierHuman;

			//FallMultiplier
			if(ev.DamageType == DamageTypes.Falldown)
				ev.Amount *= plugin.Config.FalldamageMultiplier;

			//SCP-939 Bleeding
			if(plugin.Config.Scp939AttackBleeding && ev.DamageType == DamageTypes.Scp939)
			{
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Bleeding>();
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Hemorrhage>();
			}

			//SCP-049-2 Effect
			if(plugin.Config.Scp0492AttackEffect && ev.DamageType == DamageTypes.Scp0492)
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Blinded>(2f);

			//SCP-106 AHP
			if(plugin.Config.Scp106SendPocketAhpAmount > 0 && ev.DamageType == DamageTypes.Scp106)
				ev.Attacker.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount;

			//CuffedMultiplier
			if(ev.Target.IsCuffed && ev.Attacker.ReferenceHub.characterClassManager.IsHuman())
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

			if(!RoundSummary.singleton._roundEnded && ev.Attacker.IsEnemy(ev.Target.Team) && ev.Attacker.IsHuman && ev.DamageType != DamageTypes.Contain)
				DamagesDict[ev.Attacker.Nickname] += (uint)ev.Amount;

			Log.Debug($"[OnHurting:After] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);
		}
		public void OnDied(DiedEventArgs ev)
		{
			if(ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.Spectator || ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.None || ev.Killer == null || ev.Target == null) return;
			Log.Debug($"[OnDied] {ev.Killer.Nickname}[{ev.Killer.Role}] -{ev.HitInformations.GetDamageName()}-> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.Instance.Config.IsDebugged);
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

			if(plugin.Config.Scp049StackBody && ev.HitInformations.GetDamageType() == DamageTypes.Scp049)
			{
				if(plugin.Config.Scp049CureAhpAmount > 0)
				{
					ev.Killer.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp049CureAhpAmount;
					ev.Killer.ReferenceHub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(
						ev.Killer.ReferenceHub.playerStats.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp049CureAhpAmount,
						0,
						ev.Killer.ReferenceHub.playerStats.maxArtificialHealth
					);
				}
				scp049stackAmount++;
			}

			//SCPsRecovery
			switch(ev.Killer.Role)
			{
				case RoleType.Scp173:
					ev.Killer.Health = Mathf.Clamp(ev.Killer.Health + plugin.Config.Scp173RecoveryAmount, 0, ev.Killer.MaxHealth);
					break;
				case RoleType.Scp106:
					ev.Killer.Health = Mathf.Clamp(ev.Killer.Health + plugin.Config.Scp106RecoveryAmount, 0, ev.Killer.MaxHealth);
					break;
				case RoleType.Scp096:
					ev.Killer.Health = Mathf.Clamp(ev.Killer.Health + plugin.Config.Scp096RecoveryAmount, 0, ev.Killer.MaxHealth);
					break;
				case RoleType.Scp0492:
					ev.Killer.Health = Mathf.Clamp(ev.Killer.Health + plugin.Config.Scp0492RecoveryAmount, 0, ev.Killer.MaxHealth);
					break;
				case RoleType.Scp93953:
				case RoleType.Scp93989:
					ev.Killer.Health = Mathf.Clamp(ev.Killer.Health + plugin.Config.Scp939RecoveryAmount, 0, ev.Killer.MaxHealth);
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

				if(Player.List.Any(x => x.Role == RoleType.Scp079) && Player.List.Count(x => x.Team == Team.SCP && x != ev.Target) == 1
					&& Generator079.mainGenerator.totalVoltage < 4 && !Generator079.mainGenerator.forcedOvercharge && damageTypes != DamageTypes.Nuke)
					str = str
						.Replace("{-1}", "\n全てのSCPオブジェクトの安全が確保されました。SCP-079の再収容手順を開始します。\n重度収容区画は約一分後にオーバーチャージされます。")
						.Replace("{-2}", "\nAll SCP subject has been secured. SCP-079 recontainment sequence commencing.\nHeavy containment zone will overcharge in t-minus 1 minutes.");
				else
					str = str
						.Replace("{-1}", string.Empty)
						.Replace("{-2}", string.Empty);

				Methods.SendSubtitle(str, (ushort)(str.Contains("t-minus") ? 30 : 10));
			}

			if(!RoundSummary.singleton._roundEnded && ev.Killer != ev.Target && ev.Killer.IsEnemy(ev.Target.Team))
				KillsDict[ev.Killer.Nickname] += 1;
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.DataEnabled)
				foreach(var player in Player.List)
					if(player.Role == RoleType.Scp106 && PlayerDataManager.playersData.ContainsKey(player.UserId))
						PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpKill);

			foreach(var player in Player.List.Where(x => x.Role == RoleType.Scp106))
			{
				player.Health = Mathf.Clamp(player.Health + plugin.Config.Scp106RecoveryAmount, 0, player.MaxHealth);
				player.GameObject.GetComponent<MicroHID>()?.TargetSendHitmarker(false);
				if(plugin.Config.Scp106SendPocketAhpDecayAmount > 0) player.ReferenceHub.playerStats.NetworkartificialHpDecay -= plugin.Config.Scp106SendPocketAhpDecayAmount;
			}
		}
		public void OnSyncingData(SyncingDataEventArgs ev)
		{
			if(ev.Player == null || ev.Player.IsHost || !ev.Player.ReferenceHub.Ready || ev.Player.ReferenceHub.animationController.curAnim == ev.CurrentAnimation) return;

			if(plugin.Config.Scp049StackBody
				&& ev.Player.Role == RoleType.Scp049
				&& ev.CurrentAnimation == 1 && ev.Player.ReferenceHub.animationController.curAnim != 2
				&& !ev.Player.ReferenceHub.fpc.NetworkforceStopInputs)
				if(scp049stackAmount > 0 || ev.Player.IsBypassModeEnabled)
					roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp049CureFromStack(ev.Player)));

			if(plugin.Config.Scp106Exmode
				&& ev.Player.Role == RoleType.Scp106
				&& ev.CurrentAnimation == 1 && ev.Player.ReferenceHub.animationController.curAnim != 2
				&& !ev.Player.ReferenceHub.characterClassManager.Scp106.goingViaThePortal
				&& !Warhead.IsDetonated)
				roundCoroutines.Add(Timing.RunCoroutine(
					Coroutines.Scp106CustomTeleport(ev.Player.ReferenceHub.characterClassManager.Scp106, DoorNametagExtension.NamedDoors.First(x => x.Key == "106_PRIMARY").Value.TargetDoor.transform.position + Vector3.up * 1.5f)
					));

			if(plugin.Config.StaminaCostJump > 0 && ev.CurrentAnimation == 2 && ev.Player.ReferenceHub.characterClassManager.IsHuman()
				&& !ev.Player.ReferenceHub.fpc.staminaController._invigorated.Enabled && !ev.Player.ReferenceHub.fpc.staminaController._scp207.Enabled)
			{
				ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina -= plugin.Config.StaminaCostJump;
				ev.Player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;
			}

		}
		public void OnDequippedMedicalItem(DequippedMedicalItemEventArgs ev)
		{
			Log.Debug($"[OnDequippedMedicalItem] {ev.Player.Nickname} -> {ev.Item}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.Item == ItemType.Medkit || ev.Item == ItemType.SCP500)
			{
				ev.Player.ReferenceHub.playerEffectsController.DisableEffect<Hemorrhage>();
				ev.Player.ReferenceHub.playerEffectsController.DisableEffect<Bleeding>();
			}

			if(ev.Item == ItemType.SCP500)
			{
				ev.Player.ReferenceHub.playerStats.unsyncedArtificialHealth = ev.Player.ReferenceHub.playerStats.maxArtificialHealth;
				ev.Player.ReferenceHub.fpc.ResetStamina();
			}
		}
		public void OnInteractingDoor(InteractingDoorEventArgs ev)
		{
			Log.Debug($"[OnInteractingDoor] {ev.Player.Nickname}:{ev.Door.name}:{ev.Door.RequiredPermissions.RequiredPermissions}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.InventoryKeycardActivation && ev.Player.Team != Team.SCP && !ev.Player.IsBypassModeEnabled && ev.Door.ActiveLocks == 0)
				foreach(var item in ev.Player.Inventory.items)
					if(ev.Door.RequiredPermissions.CheckPermissions(item.id, ev.Player.ReferenceHub))
						ev.IsAllowed = true;

			if(plugin.Config.AddDoorsOnSurface && ev.Door.TryGetComponent<DoorNametagExtension>(out var nametag))
			{
				if(nametag._nametag.Contains("GATE_EX_"))
				{
					bool flagL = DoorNametagExtension.NamedDoors["GATE_EX_L"].TargetDoor.AllowInteracting(ev.Player.ReferenceHub, 0);
					bool flagR = DoorNametagExtension.NamedDoors["GATE_EX_R"].TargetDoor.AllowInteracting(ev.Player.ReferenceHub, 0);
					if(flagL && flagR)
						if(nametag._nametag == "GATE_EX_L")
							DoorNametagExtension.NamedDoors["GATE_EX_R"].TargetDoor.NetworkTargetState = !DoorNametagExtension.NamedDoors["GATE_EX_R"].TargetDoor.TargetState;
						else
							DoorNametagExtension.NamedDoors["GATE_EX_L"].TargetDoor.NetworkTargetState = !DoorNametagExtension.NamedDoors["GATE_EX_L"].TargetDoor.TargetState;
					else
						ev.IsAllowed = false;
				}
			}
		}
		public void OnInteractingLocker(InteractingLockerEventArgs ev)
		{
			Log.Debug($"[OnInteractingLocker] {ev.Player.Nickname}:{ev.LockerId}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.InventoryKeycardActivation)
				foreach(var item in ev.Player.Inventory.items)
					if(ev.Player.Inventory.GetItemByID(item.id).permissions.Contains("PEDESTAL_ACC"))
						ev.IsAllowed = true;
		}
		public void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
		{
			Log.Debug($"[OnUnlockingGenerator] {ev.Player.Nickname} -> {ev.Generator.CurRoom}", SanyaPlugin.Instance.Config.IsDebugged);
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
			Log.Debug($"[OnOpeningGenerator] {ev.Player.Nickname} -> {ev.Generator.CurRoom}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.Generator.prevFinish && plugin.Config.GeneratorFinishLock)
				ev.IsAllowed = false;
		}
		public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
		{
			if(plugin.Config.TeslaTabletDisable && ev.Player.IsHuman() && ev.Player.Inventory.items.Any(x => x.id == ItemType.WeaponManagerTablet))
				ev.IsTriggerable = false;
		}

		//Scp049
		public void OnFinishingRecall(FinishingRecallEventArgs ev)
		{
			Log.Debug($"[OnFinishingRecall] {ev.Scp049.Nickname} -> {ev.Target.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			ev.Scp049.Health = Mathf.Clamp(ev.Scp049.Health + plugin.Config.Scp049RecoveryAmount, 0, ev.Scp049.MaxHealth);

			if(plugin.Config.Scp049CureAhpAmount > 0)
			{
				ev.Scp049.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp049CureAhpAmount;
				ev.Scp049.ReferenceHub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(
					ev.Scp049.ReferenceHub.playerStats.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp049CureAhpAmount,
					0,
					ev.Scp049.ReferenceHub.playerStats.maxArtificialHealth
				);
			}
		}

		//Scp079
		public void OnGainingLevel(GainingLevelEventArgs ev)
		{
			Log.Debug($"[OnGainingLevel] {ev.Player.Nickname} {ev.NewLevel}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp079ExtendEnabled)
				switch(ev.NewLevel)
				{
					case 1:
						ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079Lv2, 10);
						break;
					case 2:
						ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079Lv3, 10);
						break;
					case 3:
						ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079Lv4, 10);
						break;
					case 4:
						ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079Lv5, 10);
						break;
				}
		}

		//Scp106
		public void OnCreatingPortal(CreatingPortalEventArgs ev)
		{
			Log.Debug($"[OnCreatingPortal] {ev.Player.Nickname} -> {ev.Position}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp106PortalWithSinkhole && Sinkhole != null)
			{
				Methods.MoveNetworkIdentityObject(Sinkhole, ev.Position);
			}
		}

		//Scp914
		public void OnUpgradingItems(UpgradingItemsEventArgs ev)
		{
			Log.Debug($"[OnUpgradingItems] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp914Death)
			{
				foreach(var player in ev.Players)
				{
					player.Inventory.Clear();
					player.ReferenceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0), player.GameObject);
				}

				var coliders = Physics.OverlapBox(ev.Scp914.output.position, ev.Scp914.inputSize / 2f);
				foreach(var colider in coliders)
				{
					if(colider.TryGetComponent(out CharacterClassManager ccm))
					{
						ccm._hub.inventory.Clear();
						ccm._hub.playerStats.HurtPlayer(new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0), ccm.gameObject);
					}
				}
			}
		}
	}
}