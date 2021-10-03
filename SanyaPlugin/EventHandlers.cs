﻿using System;
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
using InventorySystem;
using InventorySystem.Configs;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Keycards;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MapGeneration;
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

		//InfoSender
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

		//ShitChecker
		internal const byte BypassFlags = (1 << 1) | (1 << 3);
		internal static readonly NetDataReader reader = new NetDataReader();
		internal static readonly NetDataWriter writer = new NetDataWriter();
		internal static readonly Dictionary<string, string> kickedbyChecker = new Dictionary<string, string>();

		//ラウンドごとの変数
		public readonly static Dictionary<int, string> connIdToUserIds = new Dictionary<int, string>();
		public readonly static Dictionary<string, uint> DamagesDict = new Dictionary<string, uint>();
		public readonly static Dictionary<string, uint> KillsDict = new Dictionary<string, uint>();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		private Vector3 nextRespawnPos = Vector3.zero;
		internal Player Overrided = null;
		internal NetworkIdentity Sinkhole = null;

		//イベント用の変数
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		private List<Team> prevSpawnQueue = null;
		private Vector3 RangePos = new Vector3(50f, 1001f, -70f);

		//ServerEvents系
		public void OnWaintingForPlayers()
		{
			//Set first loaded
			loaded = true;

			//Senderの開始/再起動
			if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation
				&& plugin.Config.InfosenderIp != "none" && plugin.Config.InfosenderPort != -1)
				sendertask = SenderAsync().StartSender();

			//プレイヤーデータの初期化
			PlayerDataManager.playersData.Clear();

			//SinkholeHazardオブジェクトの保存(いろいろ使う)
			Sinkhole = Methods.GetSinkHoleHazard();
			//SinkholeのSCP-106スポーン位置への移動(上記フィールド用に確保しておく)
			if(Sinkhole != null) Methods.MoveNetworkIdentityObject(Sinkhole, RoleType.Scp106.GetRandomSpawnProperties().Item1 - (-Vector3.down * 4));

			//前ラウンドでスポーンキューを上書きした時に戻しておく
			if(prevSpawnQueue != null)
			{
				CharacterClassManager.ClassTeamQueue.Clear();
				CharacterClassManager.ClassTeamQueue.AddRange(prevSpawnQueue);
				prevSpawnQueue = null;
			}

			//Fix maingame(11.0)
			Methods.SetAmmoConfigs();
			ReferenceHub.HostHub.characterClassManager.NetworkCurClass = RoleType.Tutorial;
			ReferenceHub.HostHub.playerMovementSync.ForcePosition(RoleType.Tutorial.GetRandomSpawnProperties().Item1);
			foreach(var gen in Recontainer079.AllGenerators)
				gen._unlockCooldownTime = gen._doorToggleCooldownTime;

			//地上脱出口の二つのドアにグレネード耐性をつける
			(DoorNametagExtension.NamedDoors["ESCAPE_PRIMARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_SECONDARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

			//AlphaWarheadの設定
			if(plugin.Config.AlphaWarheadLockAlways)
			{
				AlphaWarheadOutsitePanel.nukeside.Networkenabled = true;
				Warhead.OutsitePanel.NetworkkeycardEntered = true;
				DoorNametagExtension.NamedDoors["SURFACE_NUKE"].TargetDoor.NetworkTargetState = true;
				DoorNametagExtension.NamedDoors["SURFACE_NUKE"].TargetDoor.ServerChangeLock(DoorLockReason.AdminCommand, true);
			}

			//地上の改装（ドア置く）
			if(plugin.Config.AddDoorsOnSurface)
			{
				//Prefabの準備
				var LCZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("LCZ"));
				var EZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("EZ"));
				var HCZprefab = UnityEngine.Object.FindObjectsOfType<MapGeneration.DoorSpawnpoint>().First(x => x.TargetPrefab.name.Contains("HCZ"));

				//ドアオブジェクト作成とグレネード耐性と位置の設定
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

				//スポーンさせる
				NetworkServer.Spawn(door1.gameObject);
				NetworkServer.Spawn(door2.gameObject);
				NetworkServer.Spawn(door3.gameObject);
				NetworkServer.Spawn(door4.gameObject);
				NetworkServer.Spawn(door5.gameObject);
				NetworkServer.Spawn(door6.gameObject);
			}

			//地上の改装（ゲート移動したりステーション置いたり）
			if(plugin.Config.EditObjectsOnSurface)
			{
				//ゲートはスポーンできないので元あるやつを移動させる
				var gate = DoorNametagExtension.NamedDoors["SURFACE_GATE"].TargetDoor;
				gate.transform.localRotation = Quaternion.Euler(Vector3.up * 90f);
				(gate as PryableDoor).PrySpeed = new Vector2(1f, 0f);
				Methods.MoveNetworkIdentityObject(gate.netIdentity, new UnityEngine.Vector3(0f, 1000f, -24f));

				//ステーションのスポーン
				var stationPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Station"));
				var sportPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("sportTarget"));
				var dboyPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("dboyTarget"));

				//エレベーターA前
				var station1 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(-0.15f, 1000f, 9.75f), Quaternion.Euler(Vector3.up * 180f));
				//エレベーターB正面ドア前
				var station2 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(86.69f, 987.2f, -70.85f), Quaternion.Euler(Vector3.up));
				//MTFスポーン前
				var station3 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(147.9f, 992.77f, -46.2f), Quaternion.Euler(Vector3.up * 90f));
				//エレベーターB前
				var station4 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(83f, 992.77f, -46.35f), Quaternion.Euler(Vector3.up * 90f));
				//CIスポーン前
				var station5 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(10.37f, 987.5f, -47.5f), Quaternion.Euler(Vector3.up * 180f));
				//ゲート上
				var station6 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(56.5f, 1000f, -68.5f), Quaternion.Euler(Vector3.up * 270f));
				var station7 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(56.5f, 1000f, -71.85f), Quaternion.Euler(Vector3.up * 270f));

				//埋め立て
				var station_bigger = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(64.6f, 1000f, -68.5f), Quaternion.Euler(Vector3.zero));
				station_bigger.transform.localScale = new Vector3(10f, 6f, 15f);

				//的の設置
				var target1 = UnityEngine.Object.Instantiate(sportPrefab, new Vector3(-24.5f, 1000f, -68f), Quaternion.Euler(Vector3.up * 180f));
				var target2 = UnityEngine.Object.Instantiate(sportPrefab, new Vector3(-24.5f, 1000f, -72.5f), Quaternion.Euler(Vector3.up * 180f));
				var target3 = UnityEngine.Object.Instantiate(dboyPrefab, new Vector3(-24.5f, 1000f, -70.25f), Quaternion.Euler(Vector3.up * 180f));

				NetworkServer.Spawn(station1);
				NetworkServer.Spawn(station2);
				NetworkServer.Spawn(station3);
				NetworkServer.Spawn(station4);
				NetworkServer.Spawn(station5);
				NetworkServer.Spawn(station6);
				NetworkServer.Spawn(station7);
				NetworkServer.Spawn(station_bigger);
				NetworkServer.Spawn(target1);
				NetworkServer.Spawn(target2);
				NetworkServer.Spawn(target3);
			}

			if(plugin.Config.LightIntensitySurface != 1f)
			{
				UnityEngine.Object.FindObjectsOfType<RoomIdentifier>().First(x => x.Zone == FacilityZone.Surface).GetComponentInChildren<FlickerableLightController>().LightIntensityMultiplier = plugin.Config.LightIntensitySurface;
			}

			//イベント設定
			eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(plugin.Config.EventModeWeight.ToArray());
			switch(eventmode)
			{
				case SANYA_GAME_MODE.BLACKOUT:
					break;
				default:
					{
						eventmode = SANYA_GAME_MODE.NORMAL;
						break;
					}
			}

			if(plugin.Config.EnabledShootingRange)
				roundCoroutines.Add(Timing.CallDelayed(3f, () => {
					RoundSummary.RoundLock = true;
					CharacterClassManager.ForceRoundStart();
				}));

			Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
		}
		public void OnRoundStarted()
		{
			Log.Info($"[OnRoundStarted] Round Start!");

			if(plugin.Config.AlphaWarheadNeedElapsedSeconds != 1)
			{
				AlphaWarheadController.Host.cooldown = plugin.Config.AlphaWarheadNeedElapsedSeconds;
				AlphaWarheadController.Host.NetworktimeToDetonation += (float)AlphaWarheadController.Host.cooldown;
			}

			switch(eventmode)
			{
				case SANYA_GAME_MODE.BLACKOUT:
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.InitBlackout(), Segment.FixedUpdate));
						break;
					}
			}
		}
		public void OnRoundEnded(RoundEndedEventArgs ev)
		{
			Log.Info($"[OnRoundEnded] Round Ended. Win:{ev.LeadingTeam}");

			//プレイヤーデータの書き込み！
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

			//ラウンドが終わったら無敵にする
			if(plugin.Config.GodmodeAfterEndround)
				foreach(var player in Player.List)
					player.IsGodModeEnabled = true;

			//ランキングの作成/並び替え
			sortedDamages = DamagesDict.OrderByDescending(x => x.Value);
			sortedKills = KillsDict.OrderByDescending(x => x.Value);
		}
		public void OnRestartingRound()
		{
			Log.Info($"[OnRestartingRound] Restarting...");

			//さにゃこんぽーねんとのお掃除
			foreach(var player in Player.List)
				if(player.GameObject.TryGetComponent<SanyaPluginComponent>(out var comp))
					UnityEngine.Object.Destroy(comp);
			SanyaPluginComponent.scplists.Clear();

			//実行中のコルーチンのお掃除
			foreach(var cor in roundCoroutines)
				Timing.KillCoroutines(cor);
			roundCoroutines.Clear();

			//Connidのリセット
			connIdToUserIds.Clear();

			//ランキングのリセット
			sortedDamages = null;
			DamagesDict.Clear();
			sortedKills = null;
			KillsDict.Clear();

			//Fix maingame(11.x)
			RoundSummary.singleton.RoundEnded = true;
		}
		public void OnReloadConfigs()
		{
			Log.Debug($"[OnReloadConfigs]", SanyaPlugin.Instance.Config.IsDebugged);

			//コンフィグリロードに合わせてパースのし直し
			plugin.Config.ParseConfig();

			//Fix maingame(11.x)
			Methods.SetAmmoConfigs();
		}
		public void OnRespawningTeam(RespawningTeamEventArgs ev)
		{
			Log.Info($"[OnRespawningTeam] Queues:{ev.Players.Count} IsCI:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}");

			//ラウンド終了後にリスポーンを停止する
			if(plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
				ev.Players.Clear();

			//ランダムでリスポーン位置を変更する
			if(plugin.Config.RandomRespawnPosPercent > 0)
			{
				int randomnum = UnityEngine.Random.Range(0, 100);
				Log.Debug($"[RandomRespawnPos] Check:{randomnum}<{plugin.Config.RandomRespawnPosPercent}", SanyaPlugin.Instance.Config.IsDebugged);
				if(randomnum < plugin.Config.RandomRespawnPosPercent && !Warhead.IsDetonated && !Warhead.IsInProgress)
				{
					List<Vector3> poslist = new List<Vector3>();
					poslist.Add(RoleType.Scp096.GetRandomSpawnProperties().Item1);
					poslist.Add(RoleType.Scp049.GetRandomSpawnProperties().Item1);
					poslist.Add(RoleType.Scp93953.GetRandomSpawnProperties().Item1);

					if(!Map.IsLczDecontaminated && DecontaminationController.Singleton._nextPhase < 3)
					{
						poslist.Add(RoleType.Scp173.GetRandomSpawnProperties().Item1);
						poslist.Add(RoleType.ClassD.GetRandomSpawnProperties().Item1);

						poslist.Add(Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.LczArmory).Position);

						poslist.Add(GameObject.FindGameObjectsWithTag("RoomID").First(x => x.GetComponent<Rid>()?.id == "LC_914_CR").transform.position);
					}

					foreach(GameObject roomid in GameObject.FindGameObjectsWithTag("RoomID"))
					{
						Rid rid = roomid.GetComponent<Rid>();
						if(rid != null && (rid.id == "LC_ARMORY" || rid.id == "Shelter"))
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

		//MapEvents系
		public void OnAnnouncingDecontamination(AnnouncingDecontaminationEventArgs ev)
		{
			Log.Debug($"[OnAnnouncingDecontamination] {ev.Id}", SanyaPlugin.Instance.Config.IsDebugged);

			//字幕用
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

			//字幕用
			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.DecontaminationLockdown, 15);
		}
		public void OnAnnouncingNtfEntrance(AnnouncingNtfEntranceEventArgs ev)
		{
			Log.Debug($"[OnAnnouncingNtfEntrance] {ev.UnitName}-{ev.UnitNumber} {ev.ScpsLeft}left", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.EnabledShootingRange)
				ev.IsAllowed = false;
		}
		public void OnGeneratorActivated(GeneratorActivatedEventArgs ev)
		{
			Log.Debug($"[OnGeneratorActivated] {ev.Generator.GetComponentInParent<RoomIdentifier>()?.Name} ({Map.ActivatedGenerators + 1} / 3)", SanyaPlugin.Instance.Config.IsDebugged);

			//強制再収容のとき
			if(UnityEngine.Object.FindObjectOfType<Recontainer079>()._alreadyRecontained) 
				return;

			if(plugin.Config.GeneratorFix)
				ev.Generator.ServerSetFlag(MapGeneration.Distributors.Scp079Generator.GeneratorFlags.Open, false);

			if(plugin.Config.CassieSubtitle)
				if(Map.ActivatedGenerators == 2)
					Methods.SendSubtitle(Subtitles.GeneratorComplete, 15);
				else
					Methods.SendSubtitle(Subtitles.GeneratorFinish.Replace("{0}", (Map.ActivatedGenerators + 1).ToString()), 10);

			switch(eventmode)
			{
				case SANYA_GAME_MODE.BLACKOUT:
					{
						if(Map.ActivatedGenerators == 1)
						{
							foreach(var i in FlickerableLightController.Instances)
								i.LightIntensityMultiplier = 1f;
							if(plugin.Config.LightIntensitySurface != 1f)
								UnityEngine.Object.FindObjectsOfType<RoomIdentifier>().First(x => x.Zone == FacilityZone.Surface).GetComponentInChildren<FlickerableLightController>().LightIntensityMultiplier = plugin.Config.LightIntensitySurface;
						}

						break;
					}
			}
		}
		public void OnPlacingBulletHole(PlacingBulletHole ev)
		{
			Log.Debug($"[OnPlacingBulletHole]", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.EnabledShootingRange)
				ev.IsAllowed = false;
		}

		//WarheadEvents
		public void OnStarting(StartingEventArgs ev)
		{
			Log.Debug($"[OnStarting] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			if(AlphaWarheadController.Host.RealDetonationTime() < AlphaWarheadController.Host.timeToDetonation)
				ev.IsAllowed = false;

			//ホスト以外が開始できないように
			if(plugin.Config.EnabledShootingRange && !ev.Player.IsHost)
				ev.IsAllowed = false;

			//字幕用
			if(plugin.Config.CassieSubtitle && ev.IsAllowed)
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

			//サーバー以外が止められないようにする
			if(plugin.Config.AlphaWarheadLockAlways && !ev.Player.IsHost)
				ev.IsAllowed = false;

			//字幕用
			if(plugin.Config.CassieSubtitle && ev.IsAllowed)
				Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
		}
		public void OnChangingLeverStatus(ChangingLeverStatusEventArgs ev)
		{
			Log.Debug($"[OnChangingLeverStatus] {ev.Player.Nickname} {ev.CurrentState} -> {!ev.CurrentState}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.AlphaWarheadLockAlways)
				ev.IsAllowed = false;
		}
		public void OnDetonated()
		{
			Log.Info($"[OnDetonated] Detonated:{RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}");

			if(plugin.Config.EnabledShootingRange)
			{
				PlayerStats._singleton.Roundrestart();
			}
		}

		//PlayerEvents
		public void OnPreAuthenticating(PreAuthenticatingEventArgs ev)
		{
			Log.Debug($"[OnPreAuthenticating] {ev.Request.RemoteEndPoint.Address}:{ev.UserId}", SanyaPlugin.Instance.Config.IsDebugged);

			//PreLoad PlayersData
			if(plugin.Config.DataEnabled && !PlayerDataManager.playersData.ContainsKey(ev.UserId))
				PlayerDataManager.playersData.Add(ev.UserId, PlayerDataManager.LoadPlayerData(ev.UserId));

			//Staffs or BypassFlags
			if(ev.UserId.Contains("@northwood") || (ev.Flags & BypassFlags) > 0)
			{
				Log.Warn($"[OnPreAuthenticating] User have bypassflags. {ev.UserId}");
				return;
			}

			//VPNCheck
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

			//SteamCheck
			if((plugin.Config.KickSteamLimited || plugin.Config.KickSteamVacBanned) && ev.UserId.Contains("@steam"))
				roundCoroutines.Add(Timing.RunCoroutine(ShitChecker.CheckSteam(ev.UserId), Segment.FixedUpdate));
		}
		public void OnVerified(VerifiedEventArgs ev)
		{
			Log.Info($"[OnVerified] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			//LoadPlayersData
			if(plugin.Config.DataEnabled && !PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
				PlayerDataManager.playersData.Add(ev.Player.UserId, PlayerDataManager.LoadPlayerData(ev.Player.UserId));

			//ShitChecker
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

			//LevelBadge
			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled
				&& PlayerDataManager.playersData.TryGetValue(ev.Player.UserId, out PlayerData data))
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate));

			//MOTD
			if(!string.IsNullOrEmpty(plugin.Config.MotdMessageOnDisabledChat) && plugin.Config.DisableChatBypassWhitelist && !WhiteList.IsOnWhitelist(ev.Player.UserId))
				ev.Player.SendReportText(plugin.Config.MotdMessageOnDisabledChat.Replace("[name]", ev.Player.Nickname));
			else if(!string.IsNullOrEmpty(plugin.Config.MotdMessage))
				Methods.SendSubtitle(plugin.Config.MotdMessage.Replace("[name]", ev.Player.Nickname), 10, ev.Player);

			//DisableNTFOrder
			if(plugin.Config.PlayersInfoDisableFollow)
				ev.Player.ReferenceHub.nicknameSync.Network_playerInfoToShow = PlayerInfoArea.Nickname | PlayerInfoArea.Badge | PlayerInfoArea.CustomInfo | PlayerInfoArea.Role;

			//Component
			if(!ev.Player.GameObject.TryGetComponent<SanyaPluginComponent>(out _))
				ev.Player.GameObject.AddComponent<SanyaPluginComponent>();

			//各種Dict
			if(!connIdToUserIds.TryGetValue(ev.Player.Connection.connectionId, out _))
				connIdToUserIds.Add(ev.Player.Connection.connectionId, ev.Player.UserId);
			if(!DamagesDict.TryGetValue(ev.Player.Nickname, out _))
				DamagesDict.Add(ev.Player.Nickname, 0);
			if(!KillsDict.TryGetValue(ev.Player.Nickname, out _))
				KillsDict.Add(ev.Player.Nickname, 0);
		}
		public void OnDestroying(DestroyingEventArgs ev)
		{
			Log.Info($"[OnDestroying] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			//プレイヤーデータのアンロード
			if(plugin.Config.DataEnabled && !string.IsNullOrEmpty(ev.Player.UserId))
				if(PlayerDataManager.playersData.ContainsKey(ev.Player.UserId))
					PlayerDataManager.playersData.Remove(ev.Player.UserId);
		}
		public void OnChangingRole(ChangingRoleEventArgs ev)
		{
			if(ev.Player.Nickname == null) return;
			Log.Debug($"[OnChangingRole] {ev.Player.Nickname} [{ev.Player.ReferenceHub.characterClassManager._prevId}] -> [{ev.NewRole}] ({ev.Reason})", SanyaPlugin.Instance.Config.IsDebugged);

			//おーばーらいど！
			if(Overrided != null && Overrided == ev.Player && RoundSummary.roundTime < 3)
			{
				if(ev.NewRole.GetTeam() != Team.SCP)
				{
					ev.NewRole = (RoleType)ReferenceHub.HostHub.characterClassManager.FindRandomIdUsingDefinedTeam(Team.SCP);
					RoundSummary.singleton.classlistStart.scps_except_zombies++;
				}
				Overrided = null;
			}

			//Effect
			if(plugin.Config.Scp939InstaKill && ev.NewRole.Is939())
				roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
				{
					ev.Player.EnableEffect<Hemorrhage>();
					ev.Player.EnableEffect<Amnesia>();
				}));
			if(plugin.Config.Scp0492GiveEffectOnSpawn && ev.NewRole == RoleType.Scp0492)
				roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
				{
					ev.Player.ChangeEffectIntensity<Scp207>(1);
					ev.Player.EnableEffect<Burned>();
					ev.Player.EnableEffect<Concussed>();
					ev.Player.EnableEffect<Deafened>();
				}));

			//SCP-106のExMode用初期化
			if(ev.NewRole == RoleType.Scp106)
				ev.Player.ReferenceHub.scp106PlayerScript.SetPortalPosition(Vector3.zero, RoleType.Scp106.GetRandomSpawnProperties().Item1 - (-Vector3.down * 4));

			//デフォルトアイテムの設定
			if(plugin.Config.DefaultitemsParsed.TryGetValue(ev.NewRole, out List<ItemType> itemconfig))
			{
				if(itemconfig.Contains(ItemType.None)) ev.Items.Clear();
				else
				{
					ev.Items.Clear();
					ev.Items.AddRange(itemconfig);
				}
			}	

			//Dクラスロールボーナス
			if(!string.IsNullOrEmpty(ev.Player.GroupName) && plugin.Config.ClassdBonusitemsForRoleParsed.TryGetValue(ev.Player.GroupName, out List<ItemType> bonusitems) && ev.NewRole == RoleType.ClassD)
				ev.Items.InsertRange(0, bonusitems);

			//こんぽーねんと
			if(SanyaPluginComponent.Instances.TryGetValue(ev.Player, out var component))
				component.OnChangingRole(ev.NewRole);

			if(plugin.Config.EnabledShootingRange)
				ev.Player.IsGodModeEnabled = true;
		}
		public void OnSpawning(SpawningEventArgs ev)
		{
			Log.Debug($"[OnSpawning] {ev.Player.Nickname}(old:{ev.Player.ReferenceHub.characterClassManager._prevId}) -{ev.RoleType}-> {ev.Position}", SanyaPlugin.Instance.Config.IsDebugged);

			//ランダムポイントが決まっている場合はそこへ移動する
			if(plugin.Config.RandomRespawnPosPercent > 0
				&& ev.Player.ReferenceHub.characterClassManager._prevId == RoleType.Spectator
				&& (ev.RoleType.GetTeam() == Team.MTF || ev.RoleType.GetTeam() == Team.CHI)
				&& nextRespawnPos != Vector3.zero)
				ev.Position = nextRespawnPos;

			if(plugin.Config.EnabledShootingRange)
				ev.Position = RangePos;

			//Fix maingame(11.x)
			foreach(var i in ev.Player.Inventory.UserInventory.Items.Values.Where(x => x.ItemTypeId.IsArmor()).Select(x => x as BodyArmor))
				i.DontRemoveExcessOnDrop = true;
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnHurting:Before] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.Name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-049-2の打撃エフェクト付与
			if(plugin.Config.Scp0492AttackEffect && ev.DamageType == DamageTypes.Scp0492)
			{
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Concussed>(3f);
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Deafened>(3f);
			}

			//SCP-049-2の攻撃力
			if(ev.DamageType == DamageTypes.Scp0492)
				ev.Amount = plugin.Config.Scp0492Damage;

			//SCP-939-XXの即死攻撃
			if(plugin.Config.Scp939InstaKill && ev.DamageType == DamageTypes.Scp939)
				ev.Amount = 93900f;

			//被拘束時のダメージ
			if(ev.Target.IsCuffed && ev.Attacker.IsHuman && (ev.Target.Team == Team.CDP || ev.Target.Team == Team.RSC))
				ev.Amount *= plugin.Config.CuffedDamageMultiplier;

			//SCPのダメージ
			if(ev.Attacker != ev.Target && ev.Target.IsScp)
			{
				switch(ev.Target.Role)
				{
					case RoleType.Scp096:
						if(ev.Target.CurrentScp is PlayableScps.Scp096 scp096 && scp096.PlayerState == PlayableScps.Scp096PlayerState.Enraging)
							ev.Amount *= plugin.Config.Scp096EnragingDamageMultiplier;
						break;
				}

				if(plugin.Config.ScpTakenDamageMultiplierParsed.TryGetValue(ev.Target.Role, out var value))
					ev.Amount *= value;
			}

			//こんぽーねんと
			if(SanyaPluginComponent.Instances.TryGetValue(ev.Target, out var component))
				component.OnDamage();

			//ダメージランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Attacker.IsEnemy(ev.Target.Team) && ev.Attacker.IsHuman && 
				ev.DamageType != DamageTypes.RagdollLess && ev.DamageType != DamageTypes.Recontainment)
				DamagesDict[ev.Attacker.Nickname] += (uint)ev.Amount;

			Log.Debug($"[OnHurting:After] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.Name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);
		}
		public void OnDied(DiedEventArgs ev)
		{
			if(ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.Spectator || ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.None || ev.Killer == null || ev.Target == null) return;
			Log.Debug($"[OnDied] {ev.Killer.Nickname}[{ev.Killer.Role}] -{ev.HitInformations.Tool.Name}-> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.Instance.Config.IsDebugged);
			var targetteam = ev.Target.ReferenceHub.characterClassManager._prevId.GetTeam();
			var targetrole = ev.Target.ReferenceHub.characterClassManager._prevId;

			//キル/デス時経験値
			if(plugin.Config.DataEnabled)
			{
				if(!string.IsNullOrEmpty(ev.Killer.UserId) && ev.Killer != ev.Target && PlayerDataManager.playersData.ContainsKey(ev.Killer.UserId))
					PlayerDataManager.playersData[ev.Killer.UserId].AddExp(plugin.Config.LevelExpKill);

				if(PlayerDataManager.playersData.ContainsKey(ev.Target.UserId))
					PlayerDataManager.playersData[ev.Target.UserId].AddExp(plugin.Config.LevelExpDeath);
			}		

			//SCP-049-2キルボーナス
			if(plugin.Config.Scp0492KillStreak && ev.Killer.Role == RoleType.Scp0492)
			{
				ev.Killer.ChangeEffectIntensity<Scp207>((byte)Mathf.Clamp(ev.Killer.GetEffectIntensity<Scp207>() + 1, 0, 4));
				ev.Killer.EnableEffect<Invigorated>(5f, true);
				ev.Killer.Heal(ev.Killer.MaxHealth);
			}

			//キルヒットマーク
			if(plugin.Config.HitmarkKilled && ev.Killer != ev.Target)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmarker(ev.Killer, 2f), Segment.FixedUpdate));

			//字幕
			if(plugin.Config.CassieSubtitle && targetteam == Team.SCP && targetrole != RoleType.Scp0492 && targetrole != RoleType.Scp079)
			{
				var damageTypes = ev.HitInformations.Tool;
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

				str = str.Replace("{-1}", string.Empty).Replace("{-2}", string.Empty);

				Methods.SendSubtitle(str, (ushort)(str.Contains("t-minus") ? 30 : 10));
			}

			//キルランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Killer != ev.Target && ev.Killer.IsEnemy(ev.Target.Team))
				KillsDict[ev.Killer.Nickname] += 1;
		}
		public void OnHandcuffing(HandcuffingEventArgs ev)
		{
			Log.Debug($"[OnHandcuffing] {ev.Cuffer.Nickname} -> {ev.Target.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//キル&チケットボーナス
			if(plugin.Config.CuffedTicketDeathToMtfCi != 0 && (ev.Target.Team == Team.MTF || ev.Target.Team == Team.CHI))
			{
				ev.IsAllowed = false;
				SpawnableTeamType team = SpawnableTeamType.None;
				switch(ev.Target.Team)
				{
					case Team.MTF:
						team = SpawnableTeamType.ChaosInsurgency;
						break;
					case Team.CHI:
						team = SpawnableTeamType.NineTailedFox;
						break;
				}
				RespawnTickets.Singleton.GrantTickets(team, plugin.Config.CuffedTicketDeathToMtfCi);
				ev.Target.Hurt(5000f, ev.Cuffer, DamageTypes.Recontainment);
			}
		}
		public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
		{
			Log.Debug($"[OnSpawningRagdoll] {ev.Owner.Nickname}:{ev.HitInformations.Tool.Name}", SanyaPlugin.Instance.Config.IsDebugged);

			//死体削除
			if(SanyaPlugin.Instance.Config.TeslaDeleteObjects && ev.HitInformations.Tool == DamageTypes.Tesla)
				ev.IsAllowed = false;
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ポケディメデス時SCP-106へ経験値
			if(plugin.Config.DataEnabled)
				foreach(var player in Player.Get(RoleType.Scp106))
					if(PlayerDataManager.playersData.ContainsKey(player.UserId))
						PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpKill);


			foreach(var player in Player.Get(RoleType.Scp106))
			{
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmarker(player, 2f), Segment.FixedUpdate));
				if(!RoundSummary.singleton.RoundEnded) KillsDict[player.Nickname] += 1;
			}
		}
		public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
		{
			if(plugin.Config.TeslaDisabledPermission != "None"
				&& ev.Player.IsHuman()
				&& ev.Player.CurrentItem != null
				&& (ev.Player.CurrentItem.Base is KeycardItem keycardItem)
				&& keycardItem.Permissions.ToString().Contains(plugin.Config.TeslaDisabledPermission))
				ev.IsTriggerable = false;
		}
		public void OnDroppingAmmo(DroppingAmmoEventArgs ev)
		{
			Log.Debug($"[OnDroppingAmmo] {ev.Player.Nickname} -> {ev.AmmoType}({ev.Amount})", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.EnabledShootingRange)
				ev.IsAllowed = false;
		}
		public void OnDroppingItem(DroppingItemEventArgs ev)
		{
			Log.Debug($"[OnDroppingAmmo] {ev.Player.Nickname} -> {ev.Item.Type}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.EnabledShootingRange)
				ev.IsAllowed = false;
		}
		public void OnUsingItem(UsingItemEventArgs ev)
		{
			Log.Debug($"[OnUsingItem] {ev.Player.Nickname} / {ev.Item.Type}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.EnabledShootingRange)
			{
				ev.IsAllowed = false;

				if(ev.Item.Type == ItemType.Adrenaline)
				{
					ev.Player.IsGodModeEnabled = false;
					foreach(var i in ev.Player.Inventory.UserInventory.ReserveAmmo.Keys.ToList())
						ev.Player.Inventory.UserInventory.ReserveAmmo[i] = 0;
					ev.Player.ClearInventory();
					ev.Player.Kill(DamageTypes.RagdollLess);
				}
				else if(ev.Item.Type == ItemType.Medkit)
				{
					foreach(var pair in InventoryItemLoader.AvailableItems.Where(x => x.Key.IsAmmo()))
						if(ev.Player.Inventory.UserInventory.ReserveAmmo.TryGetValue(pair.Key, out _))
							ev.Player.Inventory.UserInventory.ReserveAmmo[pair.Key] = InventoryLimits.GetAmmoLimit(pair.Key, ev.Player.ReferenceHub);
						else
							ev.Player.Inventory.UserInventory.ReserveAmmo.Add(pair.Key, InventoryLimits.GetAmmoLimit(pair.Key, ev.Player.ReferenceHub));
					ev.Player.Inventory.SendAmmoNextFrame = true;
				}
				else if(ev.Item.Type == ItemType.Painkillers)
					if(ev.Player.HasItem(ItemType.Adrenaline))
						ev.Player.ResetInventory(new List<ItemType>()
						{
							ItemType.GunE11SR,
							ItemType.GunCrossvec,
							ItemType.GunFSP9,
							ItemType.GunAK,
							ItemType.GunLogicer,
							ItemType.GunShotgun,
							ItemType.Medkit,
							ItemType.Painkillers
						});
					else
						ev.Player.ResetInventory(new List<ItemType>()
						{
							ItemType.GunRevolver,
							ItemType.GunCOM18,
							ItemType.GunCOM15,
							ItemType.KeycardO5,
							ItemType.GrenadeHE,
							ItemType.Medkit,
							ItemType.Adrenaline,
							ItemType.Painkillers
						});
			}
		}
		public void OnUsedItem(UsedItemEventArgs ev)
		{
			Log.Debug($"[OnUsedItem] {ev.Player.Nickname} / {ev.Item.Type}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.Item.Type == ItemType.SCP500)
			{
				ev.Player.ReferenceHub.playerStats.NetworkArtificialHpDecay = 1.2f;
				ev.Player.ReferenceHub.playerStats.SafeSetAhpValue(ev.Player.ReferenceHub.playerStats.MaxArtificialHealth);
				ev.Player.ReferenceHub.fpc.ResetStamina();
				ev.Player.EnableEffect<Invigorated>(30f);
			}

			if(ev.Item.Type == ItemType.Adrenaline || ev.Item.Type == ItemType.Painkillers)
			{
				ev.Player.ReferenceHub.fpc.ResetStamina();
			}
		}
		public void OnInteractingShootingTarget(InteractingShootingTargetEventArgs ev)
		{
			Log.Debug($"[OnInteractingShootingTarget] {ev.Player.Nickname} -> {ev.TargetButton}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.TargetButton == Exiled.API.Enums.ShootingTargetButton.Remove || ev.TargetButton == Exiled.API.Enums.ShootingTargetButton.ToggleSync)
				ev.IsAllowed = false;
		}
		public void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
		{
			Log.Debug($"[OnUnlockingGenerator] {ev.Player.Nickname} -> {ev.Generator.GetComponentInParent<RoomIdentifier>().Name}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.GeneratorFix && ev.IsAllowed)
				ev.Generator.ServerSetFlag(MapGeneration.Distributors.Scp079Generator.GeneratorFlags.Open, true);
		}
		public void OnOpeningGenerator(OpeningGeneratorEventArgs ev)
		{
			Log.Debug($"[OnOpeningGenerator] {ev.Player.Nickname} -> {ev.Generator.GetComponentInParent<RoomIdentifier>().Name}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.GeneratorFix && ev.Generator.Engaged)
				ev.IsAllowed = false;
		}
		public void OnClosingGenerator(ClosingGeneratorEventArgs ev)
		{
			Log.Debug($"[OnClosingGenerator] {ev.Player.Nickname} -> {ev.Generator.GetComponentInParent<RoomIdentifier>().Name}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.GeneratorFix && ev.Generator.Activating)
				ev.IsAllowed = false;
		}

		//Scp079
		public void OnTriggeringDoor(TriggeringDoorEventArgs ev)
		{
			Log.Debug($"[OnTriggeringDoor] {ev.Player.Nickname} -> {ev.Door.Type}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp079NeedInteractGateTier != -1 
				&& (ev.Door.Type == Exiled.API.Enums.DoorType.Scp914 || ev.Door.Type == Exiled.API.Enums.DoorType.GateA || ev.Door.Type == Exiled.API.Enums.DoorType.GateB)
				&& ev.Player.Level + 1 < plugin.Config.Scp079NeedInteractGateTier)
			{
				ev.IsAllowed = false;
				ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>()?.AddHudCenterDownText(HintTexts.Error079NotEnoughTier, 3);
			}	
		}
		public void OnLockingDown(LockingDownEventArgs ev)
		{
			Log.Debug($"[OnLockingDown] {ev.Player.Nickname} -> {ev.RoomGameObject.Name}", SanyaPlugin.Instance.Config.IsDebugged);

			bool isDestroyed = false;
			foreach(var i in Scp079Interactable.InteractablesByRoomId[ev.RoomGameObject.UniqueId].Where(x => x.type == Scp079Interactable.InteractableType.Door))
				if(i.TryGetComponent<DoorVariant>(out var door) && (door is IDamageableDoor damageableDoor) && damageableDoor.IsDestroyed)
					isDestroyed = true;

			if(isDestroyed && ev.Player.ReferenceHub.scp079PlayerScript.CurrentLDCooldown <= 0f)
			{
				foreach(var i in ev.RoomGameObject.GetComponentsInChildren<FlickerableLightController>())
					i?.ServerFlickerLights(8f);

				ev.Player.ReferenceHub.scp079PlayerScript.CurrentLDCooldown = ev.Player.ReferenceHub.scp079PlayerScript.LockdownCooldown + ev.Player.ReferenceHub.scp079PlayerScript.LockdownDuration;

				foreach(var referenceHub in ev.Player.ReferenceHub.scp079PlayerScript._referenceHub.spectatorManager.ServerCurrentSpectatingPlayers)
					ev.Player.ReferenceHub.scp079PlayerScript.TargetSetLockdownCooldown(referenceHub.networkIdentity.connectionToClient, ev.Player.ReferenceHub.scp079PlayerScript.CurrentLDCooldown);
			}

		}

		//Scp106
		public void OnCreatingPortal(CreatingPortalEventArgs ev)
		{
			Log.Debug($"[OnCreatingPortal] {ev.Player.Nickname} -> {ev.Position}", SanyaPlugin.Instance.Config.IsDebugged);

			//SinkholeをPortalに同期させる
			if(plugin.Config.Scp106PortalWithSinkhole && Sinkhole != null)
				Methods.MoveNetworkIdentityObject(Sinkhole, ev.Position);
		}

		//Scp914
		public void OnUpgradingPlayer(UpgradingPlayerEventArgs ev)
		{
			Log.Debug($"[OnUpgradingPlayer] {ev.KnobSetting} Players:{ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.Scp914Debuff)
			{
				if(ev.Player.IsScp)
				{
					while(ev.Player.Inventory.UserInventory.Items.Count > 0)
						ev.Player.Inventory.ServerRemoveItem(ev.Player.Inventory.UserInventory.Items.ElementAt(0).Key, null);

					ev.Player.ReferenceHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0, true), ev.Player.GameObject);
				}
				else
				{
					ev.Player.Inventory.ServerDropEverything();

					ev.Player.SetRole(RoleType.Scp0492, Exiled.API.Enums.SpawnReason.ForceClass, true);
					roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
					{
						ev.Player.Health = ev.Player.Health / 5f;
						ev.Player.EnableEffect<Disabled>();
						ev.Player.EnableEffect<Poisoned>();
						ev.Player.EnableEffect<Concussed>();
						ev.Player.EnableEffect<Exhausted>();
					}));
				}
			}
		}
	}
}