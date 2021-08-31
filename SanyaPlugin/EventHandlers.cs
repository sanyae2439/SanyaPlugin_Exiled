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
using InventorySystem;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Keycards;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MEC;
using Mirror;
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

		//毎秒コルーチン
		private IEnumerator<float> EverySecond()
		{
			while(true)
			{
				try
				{

				}
				catch(Exception e)
				{
					Log.Error($"[EverySecond] {e}");
				}
				//毎秒
				yield return Timing.WaitForSeconds(1f);
			}
		}

		//ラウンドごとの変数
		public readonly static Dictionary<string, uint> DamagesDict = new Dictionary<string, uint>();
		public readonly static Dictionary<string, uint> KillsDict = new Dictionary<string, uint>();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		private Vector3 nextRespawnPos = Vector3.zero;
		internal int scp049stackAmount = 0;
		internal Player Overrided = null;
		internal NetworkIdentity Sinkhole = null;

		//イベント用の変数
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
		private List<Team> prevSpawnQueue = null;

		//ServerEvents系
		public void OnWaintingForPlayers()
		{
			//Set first loaded
			loaded = true;

			//Senderの開始/再起動
			if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation
				&& plugin.Config.InfosenderIp != "none" && plugin.Config.InfosenderPort != -1)
				sendertask = SenderAsync().StartSender();

			//毎秒フラグ
			roundCoroutines.Add(Timing.RunCoroutine(EverySecond(), Segment.FixedUpdate));

			//プレイヤーデータの初期化
			PlayerDataManager.playersData.Clear();

			//(廃止予定)
			Coroutines.isAirBombGoing = false;

			//SCP-049の死体スタック量初期化
			scp049stackAmount = 0;

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

			//地上脱出口の二つのドアにグレネード耐性をつける
			(DoorNametagExtension.NamedDoors["ESCAPE_PRIMARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_SECONDARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

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
				var prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Station"));

				//エレベーターA前
				var station1 = UnityEngine.Object.Instantiate(prefab, new Vector3(-0.15f, 1000f, 9.75f), Quaternion.Euler(Vector3.up * 180f));
				//エレベーターB正面ドア前
				var station2 = UnityEngine.Object.Instantiate(prefab, new Vector3(86.69f, 987.2f, -70.85f), Quaternion.Euler(Vector3.up));
				//MTFスポーン前
				var station3 = UnityEngine.Object.Instantiate(prefab, new Vector3(147.9f, 992.77f, -46.2f), Quaternion.Euler(Vector3.up * 90f));
				//エレベーターB前
				var station4 = UnityEngine.Object.Instantiate(prefab, new Vector3(83f, 992.77f, -46.35f), Quaternion.Euler(Vector3.up * 90f));
				//CIスポーン前
				var station5 = UnityEngine.Object.Instantiate(prefab, new Vector3(10.37f, 987.5f, -47.5f), Quaternion.Euler(Vector3.up * 180f));
				//ゲート上
				var station6 = UnityEngine.Object.Instantiate(prefab, new Vector3(56.5f, 1000f, -68.5f), Quaternion.Euler(Vector3.up * 270f));
				var station7 = UnityEngine.Object.Instantiate(prefab, new Vector3(56.5f, 1000f, -71.85f), Quaternion.Euler(Vector3.up * 270f));


				var station_bigger = UnityEngine.Object.Instantiate(prefab, new Vector3(64.6f, 1000f, -68.5f), Quaternion.Euler(Vector3.zero));
				station_bigger.transform.localScale = new Vector3(10f, 6f, 15f);

				NetworkServer.Spawn(station1);
				NetworkServer.Spawn(station2);
				NetworkServer.Spawn(station3);
				NetworkServer.Spawn(station4);
				NetworkServer.Spawn(station5);
				NetworkServer.Spawn(station6);
				NetworkServer.Spawn(station7);
				NetworkServer.Spawn(station_bigger);
			}

			//イベント設定
			eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(plugin.Config.EventModeWeight.ToArray());
			switch(eventmode)
			{
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
		}
		public void OnRoundEnded(RoundEndedEventArgs ev)
		{
			Log.Info($"[OnRoundEnded] Round Ended.");

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

			//？
			Coroutines.isAirBombGoing = false;

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
			Log.Debug($"[OnRespawningTeam] Queues:{ev.Players.Count} IsCI:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}", SanyaPlugin.Instance.Config.IsDebugged);

			//AlphaWarhead起爆後/ラウンド終了後にリスポーンを停止する
			if(plugin.Config.StopRespawnAfterDetonated && Warhead.IsDetonated || plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
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

		//WarheadEvents
		public void OnStarting(StartingEventArgs ev)
		{
			Log.Debug($"[OnStarting] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//字幕用
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

			//字幕用
			if(plugin.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
		}
		public void OnDetonated()
		{
			Log.Info($"[OnDetonated] Detonated:{RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}");
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

			//VCLimitter
			if(plugin.Config.DisableAllChat)
				if(!plugin.Config.DisableChatBypassWhitelist || !WhiteList.IsOnWhitelist(ev.Player.UserId))
					ev.Player.IsMuted = true;

			//MOTD
			if(!string.IsNullOrEmpty(plugin.Config.MotdMessageOnDisabledChat) && plugin.Config.DisableChatBypassWhitelist && !WhiteList.IsOnWhitelist(ev.Player.UserId) && ev.Player.IsMuted)
				ev.Player.SendReportText(plugin.Config.MotdMessageOnDisabledChat.Replace("[name]", ev.Player.Nickname));
			else if(!string.IsNullOrEmpty(plugin.Config.MotdMessage))
				Methods.SendSubtitle(plugin.Config.MotdMessage.Replace("[name]", ev.Player.Nickname), 10, ev.Player);

			//DisableNTFOrder
			if(plugin.Config.PlayersInfoDisableFollow)
				ev.Player.ReferenceHub.nicknameSync.Network_playerInfoToShow = PlayerInfoArea.Nickname | PlayerInfoArea.Badge | PlayerInfoArea.CustomInfo | PlayerInfoArea.Role;

			//MuteFixer
			foreach(var player in Player.List.Where(x => x.IsMuted))
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
			Log.Info($"[OnDestroying] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			//SCPが抜けると観戦者の誰かに置き換える
			if(plugin.Config.ReplaceScpsWhenDisconnect && ev.Player.Team == Team.SCP && ev.Player.Role != RoleType.Scp0492 && RoundSummary.RoundInProgress())
			{
				Log.Info($"[ReplaceScps] Role:{ev.Player.Role} Health:{ev.Player.Health} Pos:{ev.Player.Position}{(ev.Player.Role == RoleType.Scp079 ? $" Level079:{ev.Player.Level} Mana079:{ev.Player.Energy}/{ev.Player.MaxEnergy}" : string.Empty)}");
				if(RoundSummary.singleton.CountRole(RoleType.Spectator) > 0)
				{
					Player target = Player.Get(RoleType.Spectator).Random();
					Log.Info($"[ReplaceScps] target found:{target.Nickname}/{target.Role}");
					target.SetRole(ev.Player.Role, Exiled.API.Enums.SpawnReason.ForceClass, true);
					target.Health = ev.Player.Health;
					target.Position = ev.Player.Position;
					if(ev.Player.Role == RoleType.Scp079)
					{
						target.Level = ev.Player.Level;
						target.Energy = ev.Player.Energy;
						target.MaxEnergy = ev.Player.MaxEnergy;
						target.Camera = ev.Player.Camera;
					}
					if(target.ReferenceHub.TryGetComponent<SanyaPluginComponent>(out var sanya))
						sanya.AddHudBottomText($"<color=#bbee00><size=25>{ev.Player.ReferenceHub.characterClassManager.CurRole.fullName}のプレイヤーが切断したため、代わりとして選ばれました。</size></color>", 5);
				}
				else
					Log.Warn("[ReplaceScps] No target spectators, skipped");
			}

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
			if(Overrided != null && Overrided == ev.Player)
			{
				if(ev.NewRole.GetTeam() != Team.SCP)
				{
					ev.NewRole = (RoleType)ReferenceHub.HostHub.characterClassManager.FindRandomIdUsingDefinedTeam(Team.SCP);
					RoundSummary.singleton.classlistStart.scps_except_zombies++;
				}
				Overrided = null;
			}

			//ExModeの通知
			if(plugin.Config.ExHudEnabled && plugin.Config.Scp049StackBody && ev.NewRole == RoleType.Scp049)
				roundCoroutines.Add(Timing.CallDelayed(3f, Segment.FixedUpdate, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend049First, 10)));
			if(plugin.Config.ExHudEnabled && plugin.Config.Scp106Exmode && ev.NewRole == RoleType.Scp106)
				roundCoroutines.Add(Timing.CallDelayed(3f, Segment.FixedUpdate, () => ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(HintTexts.Extend106First, 10)));

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


			//Fix maingame(11.x)
			foreach(var i in ev.Player.Inventory.UserInventory.Items.Values.Where(x => x.ItemTypeId.IsArmor()).Select(x => x as BodyArmor))
				i.DontRemoveExcessOnDrop = true;
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Attacker.Role == RoleType.Spectator || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnHurting:Before] {ev.Attacker.Nickname}[{ev.Attacker.Role}] -{ev.Amount}({ev.DamageType.Name})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-049-2の打撃エフェクト付与
			if(plugin.Config.Scp0492AttackEffect && ev.DamageType == DamageTypes.Scp0492)
			{
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Concussed>(5f);
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Deafened>(5f);
				ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Disabled>(5f);
			}

			//被拘束時のダメージ
			if(ev.Target.IsCuffed && ev.Attacker.IsHuman && (ev.Target.Team == Team.CDP || ev.Target.Team == Team.RSC))
				ev.Amount *= plugin.Config.CuffedDamageMultiplier;

			//SCPのダメージ
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

			//ダメージランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Attacker.IsEnemy(ev.Target.Team) && ev.Attacker.IsHuman && ev.DamageType != DamageTypes.RagdollLess)
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

			//SCP-049 ExMode
			if(plugin.Config.Scp049StackBody && ev.HitInformations.Tool == DamageTypes.Scp049)
			{
				scp049stackAmount++;
			}

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
		public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
		{
			Log.Debug($"[OnSpawningRagdoll] {ev.Owner.Nickname}:{ev.HitInformations.Tool.Name}", SanyaPlugin.Instance.Config.IsDebugged);

			//死体削除
			if(SanyaPlugin.Instance.Config.Scp049StackBody && ev.HitInformations.Tool == DamageTypes.Scp049 || SanyaPlugin.Instance.Config.TeslaDeleteObjects && ev.HitInformations.Tool == DamageTypes.Tesla)
				ev.IsAllowed = false;
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ポケディメデス時SCP-106へ経験値
			if(plugin.Config.DataEnabled)
				foreach(var player in Player.List)
					if(player.Role == RoleType.Scp106 && PlayerDataManager.playersData.ContainsKey(player.UserId))
						PlayerDataManager.playersData[player.UserId].AddExp(plugin.Config.LevelExpKill);
		}
		public void OnSyncingData(SyncingDataEventArgs ev)
		{
			//同じアニメーションは無視する
			if(ev.Player == null || ev.Player.IsHost || !ev.Player.ReferenceHub.Ready || ev.Player.ReferenceHub.animationController.curAnim == ev.CurrentAnimation) return;

			if(plugin.Config.Scp049StackBody
				&& ev.Player.Role == RoleType.Scp049
				&& ev.CurrentAnimation == 1 && ev.Player.ReferenceHub.animationController.curAnim != 2
				&& !ev.Player.ReferenceHub.fpc.NetworkforceStopInputs
				&& (scp049stackAmount > 0 || ev.Player.IsBypassModeEnabled))
					roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp049CureFromStack(ev.Player), Segment.FixedUpdate));

			if(plugin.Config.Scp106Exmode
				&& ev.Player.Role == RoleType.Scp106
				&& ev.CurrentAnimation == 1 && ev.Player.ReferenceHub.animationController.curAnim != 2
				&& !ev.Player.ReferenceHub.characterClassManager.Scp106.goingViaThePortal
				&& !Warhead.IsDetonated)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp106CustomTeleport(
					ev.Player.ReferenceHub.characterClassManager.Scp106,
					DoorNametagExtension.NamedDoors.First(x => x.Key == "106_PRIMARY").Value.TargetDoor.transform.position + Vector3.up * 1.5f), Segment.FixedUpdate));

			//ジャンプ時スタミナ消費
			if(plugin.Config.StaminaCostJump > 0 
				&& ev.CurrentAnimation == 2 
				&& ev.Player.ReferenceHub.characterClassManager.IsHuman()
				&& !ev.Player.ReferenceHub.fpc.staminaController._invigorated.IsEnabled
				&& !ev.Player.ReferenceHub.fpc.staminaController._scp207.IsEnabled)
			{
				ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina -= plugin.Config.StaminaCostJump;
				ev.Player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;
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

				var coliders = Physics.OverlapBox(ev.OutputPosition, Vector3.one * Exiled.API.Features.Scp914.Scp914Controller._chamberSize / 2f);
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
			}
		}
	}
}