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
using Respawning;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
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
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb(), Segment.FixedUpdate));
						detonatedDuration = -1;
					}


					//SCPAutoNuke
					if(plugin.Config.WarheadAutoWhenNoScps > 0
						&& RoundSummary.RoundInProgress()
						&& RoundSummary.roundTime > 5
						&& !ScpAutoWarheadUsed
						&& AlphaWarheadController.Host._autoDetonate 
						&& AlphaWarheadController.Host._autoDetonateTimer >= 0f
						&& RoundSummary.singleton.CountTeam(Team.SCP) == 0)
					{
						roundCoroutines.Add(Timing.CallDelayed(plugin.Config.WarheadAutoWhenNoScps, Segment.FixedUpdate, () => {
							AlphaWarheadController.Host._autoDetonateTimer = -1f;
						}));
						ScpAutoWarheadUsed = true;
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

		/** Flag Params **/
		private int detonatedDuration = -1;

		/** RoundVar **/
		public readonly static Dictionary<string, uint> DamagesDict = new Dictionary<string, uint>();
		public readonly static Dictionary<string, uint> KillsDict = new Dictionary<string, uint>();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		internal bool IsEnableBlackout = false;
		private Vector3 nextRespawnPos = Vector3.zero;
		private Camera079 last079cam = null;
		internal int scp049stackAmount = 0;
		internal Player Overrided = null;
		public bool FriendlyFlashEnabled = false;
		internal NetworkIdentity Sinkhole = null;
		private bool ScpAutoWarheadUsed = false;

		/** EventModeVar **/
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		internal static List<FlickerableLightController> flickerableLightControllers = new List<FlickerableLightController>();
		internal static float currentIntensity = 1f;
		private List<Team> prevSpawnQueue = null;

		//ServerEvents
		public void OnWaintingForPlayers()
		{
			loaded = true;

			if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation
				&& plugin.Config.InfosenderIp != "none" && plugin.Config.InfosenderPort != -1)
				sendertask = SenderAsync().StartSender();

			roundCoroutines.Add(Timing.RunCoroutine(EverySecond(), Segment.FixedUpdate));

			PlayerDataManager.playersData.Clear();
			Coroutines.isAirBombGoing = false;

			detonatedDuration = -1;
			IsEnableBlackout = false;
			ScpAutoWarheadUsed = false;

			flickerableLightControllers.Clear();
			flickerableLightControllers.AddRange(UnityEngine.Object.FindObjectsOfType<FlickerableLightController>());

			last079cam = null;
			scp049stackAmount = 0;

			Methods.Add914RecipeCoin();
			FriendlyFlashEnabled = GameCore.ConfigFile.ServerConfig.GetBool("friendly_flash", false);
			Sinkhole = Methods.GetSinkHoleHazard();
			if(Sinkhole != null) Methods.MoveNetworkIdentityObject(Sinkhole, RoleType.Scp106.GetRandomSpawnPointForConflict() - (-Vector3.down * 4));
			if(prevSpawnQueue != null)
			{
				CharacterClassManager.ClassTeamQueue.Clear();
				CharacterClassManager.ClassTeamQueue.AddRange(prevSpawnQueue);
				prevSpawnQueue = null;
			}

			(DoorNametagExtension.NamedDoors["ESCAPE_PRIMARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_SECONDARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

			if(plugin.Config.RandomOpenNotPermissionDoors != -1f)
			{
				int counter = 0;
				foreach(var door in Map.Doors.Where(x => x.RequiredPermissions.RequiredPermissions == KeycardPermissions.None && x.GetComponentInParent<CheckpointDoor>() == null))
					if(UnityEngine.Random.Range(0, 100) < plugin.Config.RandomOpenNotPermissionDoors)
					{
						counter++;
						door.NetworkTargetState = true;
					}
				Log.Info($"[RandomDoorOpener] {counter} doors opened.");
			}

			if(plugin.Config.EditMapOnSurface)
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

				var door5 = UnityEngine.Object.Instantiate(HCZprefab.TargetPrefab, new UnityEngine.Vector3(0f, 1000f, 4.8f), Quaternion.Euler(Vector3.zero));
				(door5 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				door5.transform.localScale = new Vector3(2f, 2.05f, 1f);

				var door6 = UnityEngine.Object.Instantiate(HCZprefab.TargetPrefab, new UnityEngine.Vector3(86.5f, 987.15f, -67.3f), Quaternion.Euler(Vector3.zero));
				(door6 as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
				door6.transform.localScale = new Vector3(2.5f, 1.6f, 1f);

				NetworkServer.Spawn(door1.gameObject);
				NetworkServer.Spawn(door2.gameObject);
				NetworkServer.Spawn(door3.gameObject);
				NetworkServer.Spawn(door4.gameObject);
				NetworkServer.Spawn(door5.gameObject);
				NetworkServer.Spawn(door6.gameObject);

				var gate = DoorNametagExtension.NamedDoors["SURFACE_GATE"].TargetDoor;
				gate.transform.localRotation = Quaternion.Euler(Vector3.up * 90f);
				(gate as PryableDoor).PrySpeed = new Vector2(1f, 0f);
				Methods.MoveNetworkIdentityObject(gate.netIdentity, new UnityEngine.Vector3(0f, 1000f, -24f));

				var station = UnityEngine.Object.FindObjectsOfType<WorkStation>().First(x => x.transform.parent?.name == "GateA");
				station.transform.localRotation = Quaternion.Euler(Vector3.up);
				Methods.MoveNetworkIdentityObject(station.netIdentity, new UnityEngine.Vector3(86.69f, 988.37f, -70.4f));
			}

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
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						break;
					}
				case SANYA_GAME_MODE.NIGHT:
					{
						currentIntensity = 0.3f;
						break;
					}
				case SANYA_GAME_MODE.ALREADY_BREAKED:
					{
						prevSpawnQueue = CharacterClassManager.ClassTeamQueue.ToList();
						for(int i = 0; i < CharacterClassManager.ClassTeamQueue.Count; i++)
							if(CharacterClassManager.ClassTeamQueue[i] == Team.CDP || CharacterClassManager.ClassTeamQueue[i] == Team.RSC)
								CharacterClassManager.ClassTeamQueue[i] = Team.MTF;
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
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.NightModeInit(), Segment.FixedUpdate));
						break;
					}
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.ClassDInsurgencyInit(), Segment.FixedUpdate));
						break;
					}
				case SANYA_GAME_MODE.ALREADY_BREAKED:
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AlreadyBreakInit(), Segment.FixedUpdate));
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
				Log.Debug($"[RandomRespawnPos] Check:{randomnum}<{plugin.Config.RandomRespawnPosPercent}", SanyaPlugin.Instance.Config.IsDebugged);
				if(randomnum < plugin.Config.RandomRespawnPosPercent && !Warhead.IsDetonated && !Warhead.IsInProgress)
				{
					List<Vector3> poslist = new List<Vector3>();
					poslist.Add(RoleType.Scp096.GetRandomSpawnPointForConflict());
					poslist.Add(RoleType.Scp049.GetRandomSpawnPointForConflict());
					poslist.Add(RoleType.Scp93953.GetRandomSpawnPointForConflict());

					if(!Map.IsLCZDecontaminated && DecontaminationController.Singleton._nextPhase < 3)
					{
						poslist.Add(RoleType.Scp173.GetRandomSpawnPointForConflict());
						poslist.Add(RoleType.ClassD.GetRandomSpawnPointForConflict());

						poslist.Add(Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.LczArmory).Position);

						poslist.Add(GameObject.FindGameObjectsWithTag("RoomID").First(x => x.GetComponent<Rid>()?.id == "LC_914_CR").transform.position);

						foreach(var itempos in RandomItemSpawner.singleton.posIds)
						{
							if(itempos.posID == "RandomPistol" && itempos.DoorTriggerName == "372")
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							else if(itempos.posID == "RandomPistol" && itempos.DoorTriggerName == "173_ARMORY")
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							else if(itempos.posID == "toilet_keycard" && itempos.position.position.y > 1.25f && itempos.position.position.y < 1.35f)
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							else if(itempos.posID == "012_mScientist_keycard")
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							else if(itempos.posID == "Servers")
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
							else if(itempos.posID == "MicroHID")
								poslist.Add(new Vector3(itempos.position.position.x, itempos.position.position.y, itempos.position.position.z));
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
							roundCoroutines.Add(Timing.CallDelayed(9f, Segment.FixedUpdate, () => {
								foreach(var i in Map.Doors.Where(x => x.transform.position.y > -100f && x.transform.position.y < 100f))
									i.NetworkTargetState = true;
							}));
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
			{
				IsEnableBlackout = false;
				currentIntensity = 1f;
				Methods.SetAllIntensity(currentIntensity);
			}
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
			DecontaminationController.Singleton.NetworkRoundStartTime = -1.0;
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
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckVPN(ev), Segment.FixedUpdate));
			}

			if((plugin.Config.KickSteamLimited || plugin.Config.KickSteamVacBanned) && ev.UserId.Contains("@steam"))
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckSteam(ev.UserId), Segment.FixedUpdate));
		}
		public void OnVerified(VerifiedEventArgs ev)
		{
			Log.Info($"[OnVerified] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			if(plugin.Config.DataEnabled && !PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
				PlayerDataManager.playersData.Add(ev.Player.UserId, PlayerDataManager.LoadPlayerData(ev.Player.UserId));

			if(kickedbyChecker.TryGetValue(ev.Player.UserId, out var reason))
			{
				string reasonMessage = string.Empty;
				if(reason == "steam_vac")
					reasonMessage = Subtitles.VacBannedKickMessage;
				else if(reason == "steam_limited")
					reasonMessage = Subtitles.LimitedKickMessage;
				else if(reason == "steam_noprofile")
					reasonMessage = Subtitles.NoProfileKickMessage;
				else if(reason == "vpn")
					reasonMessage = Subtitles.VPNKickMessage;

				ServerConsole.Disconnect(ev.Player.Connection, reasonMessage);
				kickedbyChecker.Remove(ev.Player.UserId);
				return;
			}

			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled
				&& PlayerDataManager.playersData.TryGetValue(ev.Player.UserId, out PlayerData data))
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate));

			if(plugin.Config.DisableAllChat)
			{
				ev.Player.IsMuted = true;
				if(plugin.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(ev.Player.UserId) && !MuteHandler.QueryPersistentMute(ev.Player.UserId))
					roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () => { ev.Player.IsMuted = false; }));
			}

			if(!string.IsNullOrEmpty(plugin.Config.MotdMessageOnDisabledChat) && plugin.Config.DisableChatBypassWhitelist && !WhiteList.IsOnWhitelist(ev.Player.UserId) && ev.Player.IsMuted)
				ev.Player.SendReportText(plugin.Config.MotdMessageOnDisabledChat.Replace("[name]", ev.Player.Nickname));
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

			switch(eventmode)
			{
				case SANYA_GAME_MODE.NIGHT:
					{
						if(IsEnableBlackout)
							Methods.SetAllIntensity(currentIntensity);
						break;
					}
			}
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
				roundCoroutines.Add(Timing.CallDelayed(10f, Segment.FixedUpdate, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend079First, 10)));

			if(plugin.Config.ExHudEnabled && plugin.Config.Scp049StackBody && ev.NewRole == RoleType.Scp049)
				roundCoroutines.Add(Timing.CallDelayed(3f, Segment.FixedUpdate, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend049First, 10)));

			if(plugin.Config.ExHudEnabled && plugin.Config.Scp106Exmode && ev.NewRole == RoleType.Scp106)
				roundCoroutines.Add(Timing.CallDelayed(3f, Segment.FixedUpdate, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend106First, 10)));

			if(plugin.Config.DefaultitemsParsed.TryGetValue(ev.NewRole, out List<ItemType> itemconfig))
			{
				if(itemconfig.Contains(ItemType.None)) ev.Items.Clear();
				else
				{
					ev.Items.Clear();
					ev.Items.AddRange(itemconfig);
				}
			}

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

			//Speedup
			if(plugin.Config.Scp939SpeedupByHealthAmount && ev.NewRole.Is939())
				roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
				{
					ev.Player.ChangeEffectIntensity<Scp207>(1);
				}));
			else if(plugin.Config.Scp0492SpeedupByHealthAmount && ev.NewRole == RoleType.Scp0492)
				roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
				{
					ev.Player.ChangeEffectIntensity<Scp207>(1);
				}));

			//ScaleChanging
			Vector3 scale = Vector3.one;
			if(plugin.Config.Scp939ScaleMultiplier != 1f && ev.NewRole.Is939())
				scale = Vector3.one * plugin.Config.Scp939ScaleMultiplier;
			else if(ev.Player.Scale != Vector3.one)
				scale = Vector3.one;

			if(ev.Player.Scale != scale)
			{
				roundCoroutines.Add(Timing.CallDelayed(0.5f, Segment.FixedUpdate, () =>
				{
					Log.Debug($"Scale changed:{scale}", SanyaPlugin.Instance.Config.IsDebugged);
					ev.Player.Scale = scale;
				}));
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
				case SANYA_GAME_MODE.ALREADY_BREAKED:
					{
						if(ev.NewRole == RoleType.FacilityGuard)
						{
							if(plugin.Config.DefaultitemsParsed.TryGetValue(RoleType.NtfScientist, out List<ItemType> ntfitems))
							{
								if(ntfitems.Contains(ItemType.None)) ev.Items.Clear();
								else
								{
									ev.Items.Clear();
									ev.Items.AddRange(ntfitems);
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

			switch(eventmode)
			{
				case SANYA_GAME_MODE.CLASSD_INSURGENCY:
					{
						if(ev.RoleType == RoleType.ClassD)
						{
							ev.Player.Ammo.amount.Clear();
							foreach(var ammo in ev.Player.ReferenceHub.characterClassManager.Classes.SafeGet(RoleType.ChaosInsurgency).ammoTypes)
								ev.Player.Ammo.amount.Add(ammo);
						}
						break;
					}
				case SANYA_GAME_MODE.ALREADY_BREAKED:
					{
						if(ev.RoleType == RoleType.FacilityGuard)
						{
							ev.Position = RoleType.NtfCommander.GetRandomSpawnPointForConflict();
							ev.Player.Ammo.amount.Clear();
							foreach(var ammo in ev.Player.ReferenceHub.characterClassManager.Classes.SafeGet(RoleType.NtfScientist).ammoTypes)
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

			//Prevent079FF
			if(ev.Attacker != ev.Target && ev.Target.IsScp && ev.Attacker.Role == RoleType.Scp079)
			{
				ev.IsAllowed = false;
				return;
			}

			//GrenadeHitmark
			if(plugin.Config.HitmarkGrenade && ev.DamageType == DamageTypes.Grenade && ev.Target != ev.Attacker)
				ev.Attacker.ShowHitmarker();

			//TeslaDelete
			if(plugin.Config.TeslaDeleteObjects && ev.DamageType == DamageTypes.Tesla && ev.Target.ReferenceHub.characterClassManager.IsHuman())
				ev.Target.Inventory.Clear();

			//SCP-049-2 Effect
			if(plugin.Config.Scp0492AttackEffect && ev.DamageType == DamageTypes.Scp0492)
			{
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Concussed>(5f);
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Deafened>(5f);
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Disabled>(5f);
			}

			//SCP-106 AHP
			if(plugin.Config.Scp106SendPocketAhpAmount > 0 && ev.DamageType == DamageTypes.Scp106)
				ev.Attacker.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount;

			//CuffedMultiplier
			if(ev.Target.IsCuffed && (ev.Target.Team == Team.CDP || ev.Target.Team == Team.RSC))
				ev.Amount *= plugin.Config.CuffedDamageMultiplier;

			//SCPsMultiplier
			if(ev.Attacker != ev.Target && ev.Target.IsScp)
			{
				switch(ev.Target.Role)
				{
					case RoleType.Scp106:
						if(ev.DamageType == DamageTypes.Grenade)
							ev.Amount *= plugin.Config.Scp106GrenadeMultiplier;
						break;
					case RoleType.Scp096:
						if(ev.Target.CurrentScp is PlayableScps.Scp096 scp096 && scp096.PlayerState == PlayableScps.Scp096PlayerState.Enraging)
							ev.Amount *= plugin.Config.Scp096EnragingDamageMultiplier;
						break;
				}

				if(plugin.Config.ScpTakenDamageMultiplierParsed.TryGetValue(ev.Target.Role, out var value))
					ev.Amount *= value;
			}

			if(!RoundSummary.singleton._roundEnded && ev.Attacker.IsEnemy(ev.Target.Team) && ev.Attacker.IsHuman && ev.DamageType != DamageTypes.RagdollLess)
				DamagesDict[ev.Attacker.Nickname] += (uint)ev.Amount;

			//Speedup
			if((plugin.Config.Scp939SpeedupByHealthAmount && ev.Target.Role.Is939())
				|| (plugin.Config.Scp0492SpeedupByHealthAmount && ev.Target.Role == RoleType.Scp0492))
			{
				var percent = (int)(100f - (Mathf.Clamp01(1f - (ev.Target.ReferenceHub.playerStats.Health - ev.Amount) / (float)ev.Target.ReferenceHub.characterClassManager.CurRole.maxHP)) * 100f);
				var scp207 = ev.Target.GetEffect(Exiled.API.Enums.EffectType.Scp207);

				if(50 > percent && scp207.Intensity == 1)
					ev.Target.ReferenceHub.playerEffectsController.ChangeEffectIntensity<Scp207>(2);
			}

			//FFNotify
			if(ev.Attacker != ev.Target && !ev.Attacker.IsEnemy(ev.Target.Team))
			{
				ev.Target.GameObject.GetComponent<SanyaPluginComponent>()?.AddHudBottomText($"<color=#ffff00><size=25>味方の{ev.Attacker.Nickname}よりダメージを受けました[{ev.DamageType.name}:{Mathf.RoundToInt(ev.Amount * PlayerStats.FriendlyFireFactor)}Dmg]</size></color>", 5);
				ev.Attacker.GameObject.GetComponent<SanyaPluginComponent>()?.AddHudBottomText($"<color=#ff0000><size=25>味方の{ev.Target.Nickname}へダメージを与えました[{ev.DamageType.name}:{Mathf.RoundToInt(ev.Amount * PlayerStats.FriendlyFireFactor)}Dmg]</size></color>", 5);
			}

			//ReverseFF
			if(ev.Attacker != ev.Target && !ev.Attacker.IsEnemy(ev.Target.Team))
				ev.Attacker.Hurt(ev.Amount * PlayerStats.FriendlyFireFactor, ev.Attacker, ev.DamageType);

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
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmark(ev.Killer.GameObject.GetComponent<MicroHID>()), Segment.FixedUpdate));

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
				if(!RoundSummary.singleton._roundEnded) KillsDict[player.Nickname] += 1;
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
					roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp049CureFromStack(ev.Player), Segment.FixedUpdate));

			if(plugin.Config.Scp106Exmode
				&& ev.Player.Role == RoleType.Scp106
				&& ev.CurrentAnimation == 1 && ev.Player.ReferenceHub.animationController.curAnim != 2
				&& !ev.Player.ReferenceHub.characterClassManager.Scp106.goingViaThePortal
				&& !Warhead.IsDetonated)
				roundCoroutines.Add(Timing.RunCoroutine(
					Coroutines.Scp106CustomTeleport(ev.Player.ReferenceHub.characterClassManager.Scp106, DoorNametagExtension.NamedDoors.First(x => x.Key == "106_PRIMARY").Value.TargetDoor.transform.position + Vector3.up * 1.5f)
					, Segment.FixedUpdate));

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

			if(ev.Item == ItemType.SCP500)
			{
				ev.Player.ReferenceHub.playerStats.unsyncedArtificialHealth = ev.Player.ReferenceHub.playerStats.maxArtificialHealth;
				ev.Player.ReferenceHub.fpc.ResetStamina();
				ev.Player.EnableEffect<Invigorated>(30f);
			}

			if(ev.Item == ItemType.Adrenaline || ev.Item == ItemType.Painkillers)
			{
				ev.Player.ReferenceHub.fpc.ResetStamina();
			}
		}
		public void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
		{
			Log.Debug($"[OnUnlockingGenerator] {ev.Player.Nickname} -> {ev.Generator.CurRoom}", SanyaPlugin.Instance.Config.IsDebugged);

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
			if(plugin.Config.TeslaDisabledPermission != "None"
				&& ev.Player.IsHuman() 
				&& ev.Player.CurrentItemIndex != -1
				&& ReferenceHub.HostHub.inventory.GetItemByID(ev.Player.CurrentItem.id).permissions.Contains(plugin.Config.TeslaDisabledPermission))
				ev.IsTriggerable = false;
		}
		public void OnChangingItem(ChangingItemEventArgs ev)
		{
			//Fix maingame(10.2.2)
			if(ev.Player.ReferenceHub.weaponManager._reloadingWeapon == ev.Player.ReferenceHub.weaponManager.curWeapon
				&& ev.Player.ReferenceHub.weaponManager._reloadingWeapon != -100)
			{
				ev.Player.ReferenceHub.weaponManager._reloadingWeapon = -100;
				ev.Player.ReferenceHub.weaponManager._reloadCooldown = -1f;
			}
		}

		//Scp049
		public void OnFinishingRecall(FinishingRecallEventArgs ev)
		{
			Log.Debug($"[OnFinishingRecall] {ev.Scp049.Nickname} -> {ev.Target.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

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
				Methods.MoveNetworkIdentityObject(Sinkhole, ev.Position);
		}

		//Scp173
		public void OnBlinking(BlinkingEventArgs ev)
		{
			//Fix maingame(10.2.2)
			ev.Player.ReferenceHub?.playerMovementSync?.AddSafeTime(0.5f);
		}

		//Scp914
		public void OnUpgradingItems(UpgradingItemsEventArgs ev)
		{
			Log.Debug($"[OnUpgradingItems] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp914Debuff)
			{
				foreach(var player in ev.Players)
				{
					if(player.IsScp)
					{
						player.Inventory.Clear();
						player.ReferenceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0), player.GameObject);
					}
					else
					{
						foreach(var syncItemInfo in player.Inventory.items)
							ev.Items.Add(player.Inventory.SetPickup(syncItemInfo.id, syncItemInfo.durability, player.Position, player.CameraTransform.rotation, syncItemInfo.modSight, syncItemInfo.modBarrel, syncItemInfo.modOther, true));
						player.Inventory.Clear();
						player.SetRole(RoleType.Scp0492, true, false);
						roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
						{
							player.Health = player.Health / 5f;
							player.EnableEffect<Disabled>();
							player.EnableEffect<Poisoned>();
							player.EnableEffect<Concussed>();
							player.EnableEffect<Exhausted>();
						}));
					}
				}

				var coliders = Physics.OverlapBox(ev.Scp914.output.position, ev.Scp914.inputSize / 2f);
				foreach(var colider in coliders)
				{
					if(colider.TryGetComponent(out CharacterClassManager ccm))
					{
						ccm._hub.playerEffectsController.EnableEffect<Disabled>();
						ccm._hub.playerEffectsController.EnableEffect<Poisoned>();
						ccm._hub.playerEffectsController.EnableEffect<Concussed>();
						ccm._hub.playerEffectsController.EnableEffect<Exhausted>();
					}
				}

				if(UnityEngine.Random.Range(0, 100) < 20)
				{
					Methods.SpawnGrenade(ev.Scp914.output.position, (GRENADE_ID)UnityEngine.Random.Range(0,3));
					var pickup = ReferenceHub.HostHub.inventory.SetPickup((ItemType)UnityEngine.Random.Range((int)ItemType.KeycardJanitor, (int)ItemType.Coin + 1), 0f, ev.Scp914.output.position, Quaternion.identity, 0, 0, 0, true);
					pickup.RefreshDurability(true, true);
				}
			}
		}
	}
}