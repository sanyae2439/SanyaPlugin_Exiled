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
using Interactables.Verification;
using InventorySystem;
using InventorySystem.Items.Armor;
using LiteNetLib.Utils;
using MEC;
using MonoMod.Utils;
using PlayerStatsSystem;
using Respawning;
using RoundRestarting;
using UnityEngine;
using Utf8Json;

namespace SanyaPlugin
{
	public class EventHandlers
	{
		public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
		public readonly SanyaPlugin plugin;
		public List<CoroutineHandle> roundCoroutines = new();
		private bool loaded = false;

		//InfoSender
		private readonly UdpClient udpClient = new();
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
		internal async Task SendRoundResultASync(RoundEndedEventArgs ev)
		{
			Log.Debug($"[SendRoundResultASync] Started.", SanyaPlugin.Instance.Config.IsDebugged);

			try
			{
				RoundResultInfo info = new RoundResultInfo();

				DateTime dt = DateTime.Now;
				info.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
				info.name = ServerConsole.singleton.RefreshServerName();
				info.roundDuration = ev.ClassList.time - RoundSummary.singleton.classlistStart.time;
				info.winTeam = ev.LeadingTeam.ToString();
				info.totalSCPKill = RoundSummary.KilledBySCPs;
				info.totalSCPDeath = RoundSummary.singleton.classlistStart.scps_except_zombies - RoundSummary.SurvivingSCPs;
				info.totalSCPAmount = RoundSummary.singleton.classlistStart.scps_except_zombies;
				info.damageRank = sortedDamages.ToDictionary((x) => x.Key, (y) => y.Value);
				info.killRank = sortedKills.ToDictionary((x) => x.Key, (y) => y.Value);
				info.classdEscaped = new Dictionary<string, bool>(EscapedClassDDict);
				info.scientistEscaped = new Dictionary<string, bool>(EscapedScientistDict);

				string json = JsonSerializer.ToJsonString(info);
				byte[] sendBytes = Encoding.UTF8.GetBytes(json);
				await udpClient.SendAsync(sendBytes, sendBytes.Length, plugin.Config.InfosenderIp, plugin.Config.InfosenderPort);

				Log.Debug($"[SendRoundResultASync] Completed. Length:{sendBytes.Length}", SanyaPlugin.Instance.Config.IsDebugged);
			}
			catch(Exception e)
			{
				throw e;
			}
		}

		//ShitChecker
		internal const byte BypassFlags = (1 << 1) | (1 << 3);
		internal static readonly NetDataReader reader = new();
		internal static readonly NetDataWriter writer = new();
		internal static readonly Dictionary<string, string> kickedbyChecker = new();

		//ラウンドごとの変数
		public static readonly Dictionary<int, string> connIdToUserIds = new();
		public static readonly Dictionary<string, uint> DamagesDict = new();
		public static readonly Dictionary<string, uint> KillsDict = new();
		public static readonly Dictionary<string, bool> EscapedClassDDict = new();
		public static readonly Dictionary<string, bool> EscapedScientistDict = new();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		internal Player Overrided = null;
		internal bool nextForceEnd = false;

		//イベント用の変数
		internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;

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
			SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Clear();

			//Fix maingame(11.x)
			if(RoundRestart.UptimeRounds == 0)
				RoundRestart.UptimeRounds++;
			SpawnpointManager.FillSpawnPoints();

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

			Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
		}
		public void OnRoundStarted()
		{
			Log.Info($"[OnRoundStarted] Round Start!");

			switch(eventmode)
			{
				case SANYA_GAME_MODE.BLACKOUT:
					{
						roundCoroutines.Add(Timing.RunCoroutine(Coroutines.InitBlackout(), Segment.FixedUpdate));
						break;
					}
			}
		}
		public void OnEndingRound(EndingRoundEventArgs ev)
		{
			if(nextForceEnd)
			{
				ev.IsRoundEnded = true;
				Log.Warn($"[OnEndingRound] Recieved ForceEnd.");
				return;
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

					if(SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(player.UserId))
					{
						if(player.Role == RoleType.Spectator)
							SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[player.UserId].AddExp(plugin.Config.LevelExpLose);
						else
							SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[player.UserId].AddExp(plugin.Config.LevelExpWin);
					}
				}

				foreach(var data in SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Values)
				{
					data.lastUpdate = DateTime.Now;
					data.playingcount++;
					SanyaPlugin.Instance.PlayerDataManager.SavePlayerData(data);
				}
			}

			//ランキングの作成/並び替え
			sortedDamages = DamagesDict.OrderByDescending(x => x.Value);
			sortedKills = KillsDict.OrderByDescending(x => x.Value);

			//情報送信
			if(plugin.Config.InfosenderIp != "none" && plugin.Config.InfosenderPort != -1)
				_ = SendRoundResultASync(ev);
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

			//リセット
			nextForceEnd = false;

			//ランキングのリセット
			sortedDamages = null;
			DamagesDict.Clear();
			sortedKills = null;
			KillsDict.Clear();
			EscapedClassDDict.Clear();
			EscapedScientistDict.Clear();

			//Fix maingame(11.x)
			RoundSummary.singleton.RoundEnded = true;
		}
		public void OnReloadConfigs()
		{
			Log.Debug($"[OnReloadConfigs]", SanyaPlugin.Instance.Config.IsDebugged);

			//コンフィグリロードに合わせてパースのし直し
			plugin.Config.ParseConfig();
		}
		public void OnRespawningTeam(RespawningTeamEventArgs ev)
		{
			Log.Info($"[OnRespawningTeam] Queues:{ev.Players.Count} Team:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}");
		}

		//WarheadEvents
		public void OnStarting(StartingEventArgs ev)
		{
			Log.Debug($"[OnStarting] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//Fix maingame(11.x)
			if(AlphaWarheadController.Host.RealDetonationTime() < AlphaWarheadController.Host.timeToDetonation)
				ev.IsAllowed = false;
		}
		public void OnStopping(StoppingEventArgs ev)
		{
			Log.Debug($"[OnStopping] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//Fix maingame(11.x)
			if(AlphaWarheadController.Host.RealDetonationTime() - (AlphaWarheadController._resumeScenario == -1 ? 15f : 9f) < AlphaWarheadController.Host.timeToDetonation)
				ev.IsAllowed = false;
		}
		public void OnChangingLeverStatus(ChangingLeverStatusEventArgs ev)
		{
			Log.Debug($"[OnChangingLeverStatus] {ev.Player.Nickname} {ev.CurrentState} -> {!ev.CurrentState}", SanyaPlugin.Instance.Config.IsDebugged);

			//Fix maingame(11.x)
			if(AlphaWarheadController.Host.inProgress && ev.CurrentState)
				ev.IsAllowed = false;
		}

		//PlayerEvents
		public void OnPreAuthenticating(PreAuthenticatingEventArgs ev)
		{
			Log.Debug($"[OnPreAuthenticating] {ev.Request.RemoteEndPoint.Address}:{ev.UserId}", SanyaPlugin.Instance.Config.IsDebugged);

			//PreLoad PlayersData
			if(plugin.Config.DataEnabled && !SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(ev.UserId))
				SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Add(ev.UserId, SanyaPlugin.Instance.PlayerDataManager.LoadPlayerData(ev.UserId));

			//Staffs or BypassFlags
			if(ev.UserId.Contains("@northwood") || (ev.Flags & BypassFlags) > 0)
			{
				Log.Warn($"[OnPreAuthenticating] User have bypassflags. {ev.UserId}");
				return;
			}

			//VPNCheck
			if(!string.IsNullOrEmpty(plugin.Config.KickVpnApikey))
			{
				if(SanyaPlugin.Instance.ShitChecker.IsBlackListed(ev.Request.RemoteEndPoint.Address))
				{
					writer.Reset();
					writer.Put((byte)10);
					writer.Put(SanyaPlugin.Instance.Translation.VpnPreauthKickMessage);
					ev.Request.Reject(writer);
					return;
				}
				roundCoroutines.Add(Timing.RunCoroutine(SanyaPlugin.Instance.ShitChecker.CheckVPN(ev), Segment.FixedUpdate));
			}

			//SteamCheck
			if((plugin.Config.KickSteamLimited || plugin.Config.KickSteamVacBanned) && ev.UserId.Contains("@steam"))
				roundCoroutines.Add(Timing.RunCoroutine(SanyaPlugin.Instance.ShitChecker.CheckSteam(ev.UserId), Segment.FixedUpdate));
		}
		public void OnVerified(VerifiedEventArgs ev)
		{
			Log.Info($"[OnVerified] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			//LoadPlayersData
			if(plugin.Config.DataEnabled && !SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(ev.Player.UserId))
				SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Add(ev.Player.UserId, SanyaPlugin.Instance.PlayerDataManager.LoadPlayerData(ev.Player.UserId));

			//ShitChecker
			if(kickedbyChecker.TryGetValue(ev.Player.UserId, out var reason))
			{
				string reasonMessage = string.Empty;
				if(reason == "steam_vac")
					reasonMessage = SanyaPlugin.Instance.Translation.VacBannedKickMessage;
				else if(reason == "steam_limited")
					reasonMessage = SanyaPlugin.Instance.Translation.LimitedKickMessage;
				else if(reason == "steam_noprofile")
					reasonMessage = SanyaPlugin.Instance.Translation.NoProfileKickMessage;
				else if(reason == "vpn")
					reasonMessage = SanyaPlugin.Instance.Translation.VpnKickMessage;

				ServerConsole.Disconnect(ev.Player.Connection, reasonMessage);
				kickedbyChecker.Remove(ev.Player.UserId);
				return;
			}

			//LevelBadge
			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled && plugin.Config.LevelBadgeEnabled
				&& SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.TryGetValue(ev.Player.UserId, out PlayerData data))
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate));

			//MOTD
			if(!string.IsNullOrEmpty(plugin.Config.MotdMessageOnDisabledChat) && plugin.Config.DisableChatBypassWhitelist && !WhiteList.IsOnWhitelist(ev.Player.UserId))
				ev.Player.SendReportText(plugin.Config.MotdMessageOnDisabledChat.Replace("[name]", ev.Player.Nickname));
			else if(!string.IsNullOrEmpty(plugin.Config.MotdMessage))
				Methods.SendSubtitle(plugin.Config.MotdMessage.Replace("[name]", ev.Player.Nickname), 10, ev.Player);

			//Component
			if(!ev.Player.GameObject.TryGetComponent<SanyaPluginComponent>(out _))
				ev.Player.GameObject.AddComponent<SanyaPluginComponent>();

			//各種Dict
			if(!connIdToUserIds.TryGetValue(ev.Player.Connection.connectionId, out _))
				connIdToUserIds.Add(ev.Player.Connection.connectionId, ev.Player.UserId);
			if(!DamagesDict.TryGetValue(ev.Player.Nickname, out _) && !ev.Player.DoNotTrack)
				DamagesDict.Add(ev.Player.Nickname, 0);
			if(!KillsDict.TryGetValue(ev.Player.Nickname, out _) && !ev.Player.DoNotTrack)
				KillsDict.Add(ev.Player.Nickname, 0);
		}
		public void OnDestroying(DestroyingEventArgs ev)
		{
			Log.Info($"[OnDestroying] {ev.Player.Nickname} ({ev.Player.IPAddress}:{ev.Player.UserId})");

			//ScpSpawn
			if(plugin.Config.SpawnScpsWhenDisconnect && !RoundRestart.IsRoundRestarting && !Warhead.IsDetonated && ev.Player.Role.Team == Team.SCP && ev.Player.Role != RoleType.Scp0492 && ev.Player.Role != RoleType.Scp079)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.TryRespawnDisconnectedScp(ev.Player.Role.Type, ev.Player.Health), Segment.FixedUpdate));

			//プレイヤーデータのアンロード
			if(plugin.Config.DataEnabled && !string.IsNullOrEmpty(ev.Player.UserId))
				if(SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(ev.Player.UserId))
					SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Remove(ev.Player.UserId);
		}
		public void OnChangingRole(ChangingRoleEventArgs ev)
		{
			if(ev.Player.Nickname == null) return;
			Log.Info($"[OnChangingRole] {ev.Player.Nickname} [{ev.Player.ReferenceHub.characterClassManager._prevId}] -> [{ev.NewRole}] ({ev.Reason})");

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

			//デフォルトアイテムの設定
			if(plugin.Config.DefaultItemsParsed.TryGetValue(ev.NewRole, out List<ItemType> itemconfig))
			{
				if(itemconfig.Contains(ItemType.None)) ev.Items.Clear();
				else
				{
					ev.Items.Clear();
					ev.Items.AddRange(itemconfig);
					if(ev.Items.RemoveAll(x => x == ItemType.ParticleDisruptor) > 0) roundCoroutines.Add(Timing.CallDelayed(0.5f, () => ev.Player.AddItem(ItemType.ParticleDisruptor)));
				}
			}

			//デフォルト弾薬の設定
			if(plugin.Config.DefaultAmmosParsed.TryGetValue(ev.NewRole, out Dictionary<ItemType, ushort> ammoconfig))
			{
				if(ammoconfig.ContainsKey(ItemType.None)) ev.Ammo.Clear();
				else
				{
					ev.Ammo.Clear();
					ev.Ammo.AddRange(ammoconfig);
				}
			}
		}
		public void OnSpawning(SpawningEventArgs ev)
		{
			Log.Debug($"[OnSpawning] {ev.Player.Nickname}(old:{ev.Player.ReferenceHub.characterClassManager._prevId}) -{ev.RoleType}-> {ev.Position}", SanyaPlugin.Instance.Config.IsDebugged);

			//Fix maingame(11.x)
			foreach(var i in ev.Player.Inventory.UserInventory.Items.Values.Where(x => x.ItemTypeId.IsArmor()).Select(x => x as BodyArmor))
				i.DontRemoveExcessOnDrop = true;
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected || ev.Amount < 0f) return;
			Log.Debug($"[OnHurting] {ev.Attacker?.Nickname}[{ev.Attacker?.Role}] -({ev.Amount}:{ev.Handler.Type})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//ダメージランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Attacker != null && ev.Attacker.IsEnemy(ev.Target.Role.Team) && ev.Attacker.IsHuman && ev.Amount > 0f && DamagesDict.ContainsKey(ev.Attacker.Nickname))
				DamagesDict[ev.Attacker.Nickname] += (uint)ev.Amount;
		}
		public void OnDying(DyingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnDying] {ev.Killer?.Nickname}[{ev.Killer?.Role}] -> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.Instance.Config.IsDebugged);

			//落とさないアイテム
			var removingSerial = new List<ushort>();
			foreach(var i in ev.Target.Inventory.UserInventory.Items)
				if(plugin.Config.NoDropItemsParsed.Contains(i.Value.ItemTypeId))
					removingSerial.Add(i.Key);
			foreach(var s in removingSerial)
				ev.Target.Inventory.UserInventory.Items.Remove(s);
		}
		public void OnDied(DiedEventArgs ev)
		{
			if(ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.Spectator || ev.Target.ReferenceHub.characterClassManager._prevId == RoleType.None || ev.Target == null) return;
			Log.Debug($"[OnDied] {ev.Killer?.Nickname}[{ev.Killer?.Role}] -> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.Instance.Config.IsDebugged);

			//キラーがいない場合return
			if(ev.Killer == null) return;

			//キル/デス時経験値
			if(plugin.Config.DataEnabled)
			{
				if(!string.IsNullOrEmpty(ev.Killer.UserId) && ev.Killer != ev.Target && SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(ev.Killer.UserId))
					SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[ev.Killer.UserId].AddExp(plugin.Config.LevelExpKill);

				if(SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(ev.Target.UserId))
					SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[ev.Target.UserId].AddExp(plugin.Config.LevelExpDeath);
			}

			//キルヒットマーク
			if(plugin.Config.HitmarkKilled && ev.Killer != ev.Target)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmarker(ev.Killer, 2f), Segment.FixedUpdate));

			//キルランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Killer != ev.Target && ev.Killer.IsEnemy(ev.Target.Role.Team) && KillsDict.ContainsKey(ev.Killer.Nickname))
				KillsDict[ev.Killer.Nickname] += 1;
		}
		public void OnEscaping(EscapingEventArgs ev)
		{
			Log.Debug($"[OnEscaping] {ev.Player.Nickname} {ev.Player.Role} -> {ev.NewRole}", SanyaPlugin.Instance.Config.IsDebugged);

			//リザルト用のデータ
			if(ev.Player.Role == RoleType.ClassD && !ev.Player.DoNotTrack)
				EscapedClassDDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.ChaosConscript);
			else if(ev.Player.Role == RoleType.Scientist && !ev.Player.DoNotTrack)
				EscapedScientistDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.NtfSpecialist);
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ポケディメデス時SCP-106へ経験値
			if(plugin.Config.DataEnabled)
				foreach(var player in Player.Get(RoleType.Scp106))
					if(SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(player.UserId))
						SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[player.UserId].AddExp(plugin.Config.LevelExpKill);

			//キルランキング
			foreach(var player in Player.Get(RoleType.Scp106))
			{
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmarker(player, 2f), Segment.FixedUpdate));
				if(!RoundSummary.singleton.RoundEnded) KillsDict[player.Nickname] += 1;
			}
		}
	}
}