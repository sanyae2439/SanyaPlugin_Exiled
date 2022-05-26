using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AdminToys;
using CustomPlayerEffects;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events;
using Exiled.Events.EventArgs;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Ammo;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Usables.Scp244.Hypothermia;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MapGeneration;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using PlayerStatsSystem;
using Respawning;
using RoundRestarting;
using SanyaPlugin.Components;
using Scp914.Processors;
using UnityEngine;
using Utf8Json;
using Utils.Networking;

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
		private Vector3 nextRespawnPos = Vector3.zero;
		internal Player Overrided = null;
		internal bool nextForceEnd = false;
		internal List<SinkholeEnvironmentalHazard> Sinkholes = new();

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

			//プレイヤーデータの初期化
			SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Clear();

			//初期スポーンデータのロード(Fix maingame 11.x)
			SpawnpointManager.FillSpawnPoints();

			//SinkholeHazardオブジェクトの保存(いろいろ使う)
			Sinkholes = new List<SinkholeEnvironmentalHazard>(UnityEngine.Object.FindObjectsOfType<SinkholeEnvironmentalHazard>());
			//SinkholeのSCP-106スポーン位置への移動(上記フィールド用に確保しておく)
			foreach(var sinkhole in Sinkholes)
				Methods.MoveNetworkIdentityObject(sinkhole.netIdentity, RoleType.Scp106.GetRandomSpawnProperties().Item1 - (-Vector3.down * 4));

			//前ラウンドでスポーンキューを上書きした時に戻しておく
			if(prevSpawnQueue != null)
			{
				CharacterClassManager.ClassTeamQueue.Clear();
				CharacterClassManager.ClassTeamQueue.AddRange(prevSpawnQueue);
				prevSpawnQueue = null;
			}

			//Fix maingame(11.x)
			if(RoundRestarting.RoundRestart.UptimeRounds == 0)
				RoundRestarting.RoundRestart.UptimeRounds++;
			Methods.SetAmmoConfigs();
			ReferenceHub.HostHub.characterClassManager.NetworkCurClass = RoleType.Tutorial;
			ReferenceHub.HostHub.playerMovementSync.ForcePosition(new Vector3(0f, 2000f, 0f));
			foreach(var gen in Recontainer079.AllGenerators)
				gen._unlockCooldownTime = gen._doorToggleCooldownTime;
			RespawnWaveGenerator.SpawnableTeams[SpawnableTeamType.NineTailedFox] = new NineTailedFoxSpawnHandler(RespawnWaveGenerator.GetConfigLimit("maximum_MTF_respawn_amount", 15), 1, 17.95f, true);
			RespawnWaveGenerator.SpawnableTeams[SpawnableTeamType.ChaosInsurgency] = new ChaosInsurgencySpawnHandler(RespawnWaveGenerator.GetConfigLimit("maximum_CI_respawn_amount", 15), 1, 13.49f, false);
			(InventoryItemLoader.AvailableItems[ItemType.ArmorHeavy] as BodyArmor).HelmetEfficacy = 100;
			(InventoryItemLoader.AvailableItems[ItemType.ArmorHeavy] as BodyArmor).VestEfficacy = 100;

			//地上脱出口の二つのドアとHIDのドアにグレネード耐性をつける
			(DoorNametagExtension.NamedDoors["HID"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_PRIMARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_SECONDARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

			//地上の改装（ゲート移動したりステーション置いたり）
			if(plugin.Config.EditObjectsOnSurface)
			{
				//ゲートはスポーンできないので元あるやつを移動させる
				var gate = DoorNametagExtension.NamedDoors["SURFACE_GATE"].TargetDoor;
				gate.transform.localRotation = Quaternion.Euler(Vector3.up * 90f);
				(gate as PryableDoor).PrySpeed = new Vector2(1f, 0f);
				Methods.MoveNetworkIdentityObject(gate.netIdentity, new UnityEngine.Vector3(0f, 1000f, -24f));

				//Prefabの確保
				var primitivePrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Primitive"));
				var lightPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("LightSource"));
				var stationPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Station"));
				var sportPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("sportTarget"));
				var dboyPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("dboyTarget"));

				//エレベーターA正面
				var station1 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(15.5f, 1000f, -1.9f), Quaternion.Euler(Vector3.up * 270f));
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
				//エレベーターA正面2
				var station8 = UnityEngine.Object.Instantiate(stationPrefab, new Vector3(15.5f, 1000f, 2.75f), Quaternion.Euler(Vector3.up * 270f));

				//核起爆室のライト
				var light_nuke = UnityEngine.Object.Instantiate(lightPrefab.GetComponent<LightSourceToy>());
				light_nuke.transform.position = new Vector3(40.75f, 991f, -35.75f);
				light_nuke.NetworkLightRange = 4.5f;
				light_nuke.NetworkLightIntensity = 2f;
				light_nuke.NetworkLightColor = new Color(1f, 0f, 0f);

				//核起動室上箱
				var wall_fence = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_fence.transform.position = new Vector3(52.4f, 1001f, -57.5f);
				wall_fence.transform.localScale = new Vector3(5f, 5f, 18.5f);
				wall_fence.UpdatePositionServer();
				wall_fence.NetworkMaterialColor = Color.white;
				wall_fence.NetworkPrimitiveType = PrimitiveType.Cube;

				//SCP-106のコンテナ壁
				var room106 = RoomIdentifier.AllRoomIdentifiers.First(x => x.Name == RoomName.Hcz106);
				var wall_106_1 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_106_1.transform.SetParentAndOffset(room106.transform, new Vector3(9f, 5f, -4.5f));
				wall_106_1.transform.localScale = new Vector3(32f, 11f, 0.5f);
				if(room106.transform.forward == Vector3.left || room106.transform.forward == Vector3.right)
					wall_106_1.transform.rotation = Quaternion.Euler(Vector3.up * 90f);
				wall_106_1.UpdatePositionServer();
				wall_106_1.NetworkMaterialColor = Color.gray;
				wall_106_1.NetworkPrimitiveType = PrimitiveType.Cube;

				var wall_106_2 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_106_2.transform.SetParentAndOffset(room106.transform, new Vector3(-6.5f, 5f, -16.5f));
				wall_106_2.transform.localScale = new Vector3(1f, 11f, 24f);
				if(room106.transform.forward == Vector3.left || room106.transform.forward == Vector3.right)
					wall_106_2.transform.rotation = Quaternion.Euler(Vector3.up * 90f);
				wall_106_2.UpdatePositionServer();
				wall_106_2.NetworkMaterialColor = Color.gray;
				wall_106_2.NetworkPrimitiveType = PrimitiveType.Cube;

				//SCP-939スポーン位置の蓋
				var room939 = RoomIdentifier.AllRoomIdentifiers.First(x => x.Name == RoomName.Hcz939);
				var wall_939_1 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_939_1.transform.SetParentAndOffset(room939.transform, new Vector3(0f, -0.55f, 1.2f));
				wall_939_1.transform.localScale = new Vector3(16f, 1f, 13f);
				if(room939.transform.forward == Vector3.left || room939.transform.forward == Vector3.right)
					wall_939_1.transform.rotation = Quaternion.Euler(Vector3.up * 90f);
				wall_939_1.UpdatePositionServer();
				wall_939_1.NetworkMaterialColor = Color.gray;
				wall_939_1.NetworkPrimitiveType = PrimitiveType.Cube;

				NetworkServer.Spawn(station1);
				NetworkServer.Spawn(station2);
				NetworkServer.Spawn(station3);
				NetworkServer.Spawn(station4);
				NetworkServer.Spawn(station5);
				NetworkServer.Spawn(station6);
				NetworkServer.Spawn(station7);
				NetworkServer.Spawn(station8);
				NetworkServer.Spawn(light_nuke.gameObject);
				NetworkServer.Spawn(wall_fence.gameObject);
				NetworkServer.Spawn(wall_106_1.gameObject);
				NetworkServer.Spawn(wall_106_2.gameObject);
				NetworkServer.Spawn(wall_939_1.gameObject);
			}

			//アイテム追加
			if(plugin.Config.AddItemsOnFacility)
			{
				var hczradio = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.Radio && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;

				var hczcom18 = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.GunCOM18 && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position;
				Methods.SpawnItem(ItemType.KeycardNTFOfficer, hczcom18);

				var hczflash = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.Flashlight && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position;
				Methods.SpawnItem(ItemType.Adrenaline, hczflash);
				Methods.SpawnItem(ItemType.Medkit, hczflash);

				var lczfsp9 = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.GunFSP9 && x.TriggerDoorName == "LCZ_ARMORY")._positionVariants.First().position;
				Methods.SpawnItem(ItemType.KeycardNTFOfficer, lczfsp9);
				Methods.SpawnItem(ItemType.Medkit, lczfsp9);
				Methods.SpawnItem(ItemType.Medkit, lczfsp9);

				var hcz079crossvec = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.GunCrossvec && string.IsNullOrEmpty(x.TriggerDoorName))._positionVariants.First().position;
				var revolver1 = Methods.SpawnItem(ItemType.GunRevolver, hcz079crossvec) as FirearmPickup;
				revolver1.NetworkStatus = new FirearmStatus(2, FirearmStatusFlags.MagazineInserted, AttachmentsUtils.GetRandomAttachmentsCode(ItemType.GunRevolver));
				revolver1.Distributed = true;
				var revolver2 = Methods.SpawnItem(ItemType.GunRevolver, hcz079crossvec) as FirearmPickup;
				revolver2.NetworkStatus = new FirearmStatus(2, FirearmStatusFlags.MagazineInserted, AttachmentsUtils.GetRandomAttachmentsCode(ItemType.GunRevolver));
				revolver2.Distributed = true;

				var hcz079ammo9mm = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.Ammo9x19 && string.IsNullOrEmpty(x.TriggerDoorName))._positionVariants.First().position;
				(Methods.SpawnItem(ItemType.Ammo44cal, hcz079ammo9mm) as AmmoPickup).NetworkSavedAmmo = 12;
				(Methods.SpawnItem(ItemType.Ammo44cal, hcz079ammo9mm) as AmmoPickup).NetworkSavedAmmo = 12;
				(Methods.SpawnItem(ItemType.Ammo44cal, hcz079ammo9mm) as AmmoPickup).NetworkSavedAmmo = 12;
			}

			//SCP-914のRecipe追加
			if(plugin.Config.Scp914AddCoinRecipes)
			{
				if(InventoryItemLoader.AvailableItems.TryGetValue(ItemType.Coin, out var itemBase) && itemBase.TryGetComponent<Scp914ItemProcessor>(out var processor))
				{
					var stdpro = processor as StandardItemProcessor;
					Array.Clear(stdpro._coarseOutputs, 0, stdpro._coarseOutputs.Length);
					Array.Clear(stdpro._fineOutputs, 0, stdpro._fineOutputs.Length);
					Array.Clear(stdpro._veryFineOutputs, 0, stdpro._veryFineOutputs.Length);

					List<ItemType> coarse = new List<ItemType>
					{
						ItemType.Radio
					};
					stdpro._coarseOutputs = coarse.ToArray();

					List<ItemType> fine = new List<ItemType>
					{
						ItemType.Flashlight
					};
					stdpro._fineOutputs = fine.ToArray();

					List<ItemType> veryfine = new List<ItemType>();
					for(int i = 0; i <= (int)Enum.GetValues(typeof(ItemType)).Cast<ItemType>().Max(); i++)
						veryfine.Add((ItemType)i);
					stdpro._veryFineOutputs = veryfine.ToArray();
				}
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

			Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
		}
		public void OnRoundStarted()
		{
			Log.Info($"[OnRoundStarted] Round Start!");

			//Fix maingame(11.x)
			DecontaminationController.Singleton.NetworkRoundStartTime = NetworkTime.time;

			if(plugin.Config.Scp106ChamberLockWhenUnbreached)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.CheckScp106Chamber(), Segment.FixedUpdate));

			if(plugin.Config.ClassdPrisonInit)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.InitClassDPrison(), Segment.FixedUpdate));

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

			if(plugin.Config.RoundEndWhenNoScps && !ev.IsRoundEnded && ev.ClassList.scps_except_zombies == 0)
			{
				ev.IsRoundEnded = true;
				Log.Warn($"[OnEndingRound] Force Ended By No Scps.");
				return;
			}

			if(plugin.Config.PreventRoundEndWhenCiWithScps 
				&& ev.IsRoundEnded 
				&& (ev.ClassList.chaos_insurgents) != 0 
				&& (ev.ClassList.scps_except_zombies + ev.ClassList.zombies) != 0
			)
				ev.IsRoundEnded = false;
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

			//ラウンドが終わったら無敵にする
			if(plugin.Config.GodmodeAfterEndround)
				foreach(var player in Player.List)
					player.IsGodModeEnabled = true;

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

			//Fix maingame(11.x)
			Methods.SetAmmoConfigs();
		}
		public void OnRespawningTeam(RespawningTeamEventArgs ev)
		{
			Log.Info($"[OnRespawningTeam] Queues:{ev.Players.Count} Team:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}");

			//ラウンド終了後にリスポーンを停止する
			if(plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
				ev.Players.Clear();

			//チケットが0になったら強制終了
			if(plugin.Config.RoundEndWhenNoMtfTickets && RoundSummary.RoundInProgress() && RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox) <= 0)
			{
				nextForceEnd = true;
				ev.IsAllowed = false;
				Log.Warn($"[OnEndingRound] Force Ended By No MTF Tickets.");
				return;
			}

			//ランダムでリスポーン位置を変更する
			if(plugin.Config.RandomRespawnPosPercent > 0)
			{
				int randomnum = UnityEngine.Random.Range(0, 100);
				Log.Info($"[RandomRespawnPos] Check:{randomnum}<{plugin.Config.RandomRespawnPosPercent}");
				if(randomnum < plugin.Config.RandomRespawnPosPercent && !Warhead.IsDetonated && !Warhead.IsInProgress)
				{
					List<Vector3> poslist = new List<Vector3>();
					if(!Map.IsLczDecontaminated && DecontaminationController.Singleton._nextPhase < 3)
						poslist.Add(Exiled.API.Features.Camera.Get(Exiled.API.Enums.CameraType.Lcz173Hallway).Transform.position + Vector3.down);

					poslist.Add(RoleType.Scp93953.GetRandomSpawnProperties().Item1);
					poslist.Add(GameObject.FindGameObjectsWithTag("RoomID").First(x => x.GetComponent<Rid>()?.id == "Shelter").transform.position);
					poslist.Add(Exiled.API.Features.Camera.Get(Exiled.API.Enums.CameraType.HczWarheadArmory).Transform.position + Vector3.down);
					poslist.Add(Exiled.API.Features.Camera.Get(Exiled.API.Enums.CameraType.Hcz049Armory).Transform.position + Vector3.down);

					foreach(var i in poslist)
						Log.Debug($"[RandomRespawnPos] TargetLists:{i}", SanyaPlugin.Instance.Config.IsDebugged);

					int randomnumlast = UnityEngine.Random.Range(0, poslist.Count);
					nextRespawnPos = new Vector3(poslist[randomnumlast].x, poslist[randomnumlast].y + 2f, poslist[randomnumlast].z);

					Log.Info($"[RandomRespawnPos] Determined:{nextRespawnPos}");
				}
				else
				{
					nextRespawnPos = Vector3.zero;
				}
			}
		}

		//MapEvents
		public void OnExplodingGrenade(ExplodingGrenadeEventArgs ev)
		{
			Log.Debug($"[OnExplodingGrenade] thrower:{ev.Thrower.Nickname} type:{ev.GrenadeType} toaffect:{ev.TargetsToAffect.Count}", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-106
			if(ev.GrenadeType == Exiled.API.Enums.GrenadeType.Flashbang)
				foreach(var i in ev.TargetsToAffect)
					if(i.Role == RoleType.Scp106)
					{
						i.ArtificialHealth = 0f;
						roundCoroutines.Add(Timing.RunCoroutine(i.ReferenceHub.scp106PlayerScript._DoTeleportAnimation()));
					}
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

			//DisableNTFOrder
			if(plugin.Config.PlayersInfoDisableFollow)
				ev.Player.ReferenceHub.nicknameSync.Network_playerInfoToShow = PlayerInfoArea.Nickname | PlayerInfoArea.Badge | PlayerInfoArea.CustomInfo | PlayerInfoArea.Role;

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
					ev.Player.ChangeEffectIntensity<MovementBoost>(20);
					ev.Player.EnableEffect<Burned>();
					ev.Player.EnableEffect<Invigorated>();
				}));
			if(plugin.Config.Scp049SpeedupAmount != 0 && ev.NewRole == RoleType.Scp049)
				roundCoroutines.Add(Timing.CallDelayed(1f, Segment.FixedUpdate, () =>
				{
					ev.Player.ChangeEffectIntensity<MovementBoost>(plugin.Config.Scp049SpeedupAmount);
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
					if(ev.Items.RemoveAll(x => x == ItemType.ParticleDisruptor) > 0) roundCoroutines.Add(Timing.CallDelayed(0.5f, () => ev.Player.AddItem(ItemType.ParticleDisruptor)));
				}
			}

			//Dクラスロールボーナス
			if(!string.IsNullOrEmpty(ev.Player.GroupName) && plugin.Config.ClassdBonusitemsForRoleParsed.TryGetValue(ev.Player.GroupName, out List<ItemType> bonusitems) && ev.NewRole == RoleType.ClassD)
				ev.Items.InsertRange(0, bonusitems);

			//SCPホットキーの初期化
			if((plugin.Config.Scp106ExHotkey && ev.NewRole == RoleType.Scp106) || (plugin.Config.Scp079ExHotkey && ev.NewRole == RoleType.Scp079))
			{
				ev.Items.Clear();
				ev.Items.AddRange(new ItemType[]
				{
					ItemType.GunCOM15,
					ItemType.GunCOM18,
					ItemType.KeycardJanitor,
					ItemType.GrenadeFlash,
					ItemType.Painkillers
				});
			}

			//こんぽーねんと
			if(SanyaPluginComponent.Instances.TryGetValue(ev.Player, out var component))
				component.OnChangingRole(ev.NewRole, ev.Player.ReferenceHub.characterClassManager._prevId);
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
			if(ev.RoleType == RoleType.NtfSpecialist || (ev.Player.ReferenceHub.characterClassManager._prevId == RoleType.ClassD && ev.RoleType == RoleType.NtfPrivate))
				ev.Position = new Vector3(190f, 993.8f, -91.3f);
			if(ev.RoleType == RoleType.ChaosConscript)
				ev.Position = new Vector3(-55.7f, 988.9f, -49.4f);
			foreach(var i in ev.Player.Inventory.UserInventory.Items.Values.Where(x => x.ItemTypeId.IsArmor()).Select(x => x as BodyArmor))
				i.DontRemoveExcessOnDrop = true;
		}
		public void OnHurting(HurtingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected || ev.Amount < 0f) return;
			Log.Debug($"[OnHurting] {ev.Attacker?.Nickname}[{ev.Attacker?.Role}] -({ev.Amount}:{ev.Handler.Type})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-049の治療中ダメージ
			if(ev.Attacker != ev.Target && ev.Target.Role == RoleType.Scp049 && ev.Target.CurrentScp is PlayableScps.Scp049 scp049 && scp049._recallInProgressServer)
				ev.Amount *= plugin.Config.Scp049TakenDamageWhenCureMultiplier;

			//SCP-096の発狂中はダメージ激減
			if(plugin.Config.Scp096Rework && ev.Target.Role == RoleType.Scp096 && ev.Attacker != ev.Target && ev.Target.CurrentScp is PlayableScps.Scp096 scp096 && (scp096.Enraged || scp096.Enraging))
				ev.Amount *= 0.01f;

			//ダメージタイプ分岐
			switch(ev.Handler.Base)
			{
				case ScpDamageHandler scp:
					{
						//SCP-049-2
						if(scp._translationId == DeathTranslations.Zombie.Id)
						{
							//攻撃力
							ev.Amount = plugin.Config.Scp0492Damage;

							//打撃エフェクト付与
							if(plugin.Config.Scp0492AttackEffect && ev.Attacker?.Role == RoleType.Scp0492)
							{
								ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Concussed>(3f);
								ev.Target.ReferenceHub.playerEffectsController.EnableEffect<Deafened>(3f);
							}
						}

						//SCP-939-XX
						if(scp._translationId == DeathTranslations.Scp939.Id)
						{
							//即死攻撃
							if(plugin.Config.Scp939InstaKill)
								ev.Amount = 93900f;
						}

						break;
					}
				case FirearmDamageHandler firearm:
					{
						if(firearm.WeaponType == ItemType.GunRevolver)
						{
							ev.Amount *= plugin.Config.RevolverDamageMultiplier;
							if(plugin.Config.HandgunEffect && ev.Attacker.IsEnemy(ev.Target.Role.Team)) 
								new DisruptorHitreg.DisruptorHitMessage { 
									Position = ev.Target.Position, 
									Rotation = new LowPrecisionQuaternion(ev.Target.GameObject.transform.rotation) 
								}.SendToAuthenticated();
						}			
						if((firearm.WeaponType == ItemType.GunCOM15 || firearm.WeaponType == ItemType.GunCOM18) && plugin.Config.HandgunEffect && ev.Attacker.IsEnemy(ev.Target.Role.Team))
						{
							if(ev.Target.Role.Team == Team.SCP)
							{
								if(ev.Target.Role != RoleType.Scp096) 
									ev.Target.EnableEffect<Disabled>(5f);
								ev.Target.EnableEffect<Burned>(60f);

								if(ev.Target.ArtificialHealth > 0)
								{
									ev.Target.Health -= ev.Target.Role != RoleType.Scp106 ? ev.Amount : ev.Amount * 0.1f;
									ev.Target.Connection.Send(new HumeShieldSubEffect.HumeBlockMsg());
								}							
							}

							if(ev.Target.GameObject.TryGetComponent<LightMoveComponent>(out var lightMove))
								lightMove.Timer = 60f;
							else
								ev.Target.GameObject.AddComponent<LightMoveComponent>().Timer = 60f;

							ev.Target.EnableEffect<Concussed>(5f);
						}
						break;
					}
				case MicroHidDamageHandler _:
					{
						ev.Amount *= plugin.Config.MicrohidDamageMultiplier;
						break;
					}
				case DisruptorDamageHandler _:
					{
						if(ev.Attacker.Role.Team != Team.SCP)
							ev.Amount *= plugin.Config.DisruptorDamageMultiplier;
						break;
					}
			}

			//ヘビィアーマーの効果値
			if(ev.Target.IsHuman()
				&& (ev.Handler.Base is FirearmDamageHandler || ev.Handler.Base is ExplosionDamageHandler || ev.Handler.Base is Scp018DamageHandler)
				&& ev.Target.ReferenceHub.inventory.TryGetBodyArmor(out var bodyArmor)
				&& bodyArmor.ItemTypeId == ItemType.ArmorHeavy
			)
				ev.Amount *= plugin.Config.HeavyArmorDamageEfficacy;

			//こんぽーねんと
			if(SanyaPluginComponent.Instances.TryGetValue(ev.Target, out var component))
				component.OnDamage();

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

			//アイテム削除
			if(ev.Handler.Base is UniversalDamageHandler universal)
			{
				if(plugin.Config.PocketdimensionClean && universal.TranslationId == DeathTranslations.PocketDecay.Id
					|| plugin.Config.TeslaDeleteObjects && universal.TranslationId == DeathTranslations.Tesla.Id)
				{
					ev.Target.Ammo.Clear();
					ev.Target.Inventory.SendAmmoNextFrame = true;
					ev.Target.ClearInventory();
				}
			}

			//SCPはアイテムを落とさない
			if(ev.Target.IsScp)
				ev.Target.ClearInventory();
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

			//SCP-049-2キルボーナス
			if(plugin.Config.Scp0492KillStreak && ev.Killer.Role == RoleType.Scp0492)
			{
				ev.Killer.ChangeEffectIntensity<MovementBoost>((byte)Mathf.Clamp(ev.Killer.GetEffectIntensity<MovementBoost>() + 10, 0, 50));
				ev.Killer.EnableEffect<Invigorated>(5f, true);
				ev.Killer.Heal(ev.Killer.MaxHealth);
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

			if(ev.Player.Role == RoleType.ClassD && !ev.Player.DoNotTrack)
				EscapedClassDDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.ChaosConscript);
			else if(ev.Player.Role == RoleType.Scientist && !ev.Player.DoNotTrack)
				EscapedScientistDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.NtfSpecialist);
		}
		public void OnHandcuffing(HandcuffingEventArgs ev)
		{
			Log.Debug($"[OnHandcuffing] {ev.Cuffer.Nickname} -> {ev.Target.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//キル&チケットボーナス
			if(plugin.Config.CuffedTicketDeathToMtfCi != 0 && (ev.Target.Role.Team == Team.MTF || ev.Target.Role.Team == Team.CHI))
			{
				ev.IsAllowed = false;
				SpawnableTeamType team = SpawnableTeamType.None;
				switch(ev.Target.Role.Team)
				{
					case Team.MTF:
						team = SpawnableTeamType.ChaosInsurgency;
						break;
					case Team.CHI:
						team = SpawnableTeamType.NineTailedFox;
						break;
				}
				RespawnTickets.Singleton.GrantTickets(team, plugin.Config.CuffedTicketDeathToMtfCi);
				ev.Target.ReferenceHub.playerStats.DealDamage(new RecontainmentDamageHandler(new Footprinting.Footprint(ev.Cuffer.ReferenceHub)));
			}
		}
		public void OnJumping(JumpingEventArgs ev)
		{
			Log.Debug($"[OnJumping] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ジャンプ時スタミナ消費
			if(SanyaPlugin.Instance.Config.StaminaCostJump > 0 
				&& ev.Player.ReferenceHub.characterClassManager.IsHuman()
				&& ev.Player.ReferenceHub.characterClassManager.AliveTime > ev.Player.ReferenceHub.fpc.staminaController.StaminaImmunityUponRespawn)
			{
				float cost = SanyaPlugin.Instance.Config.StaminaCostJump;
				if(ev.Player.Inventory.TryGetBodyArmor(out BodyArmor bodyArmor))
				{
					BodyArmorUtils.GetMovementProperties(ev.Player.Role.Team, bodyArmor, out _, out float staminamulti);
					cost *= staminamulti;
				}

				if(ev.Player.Inventory.CurInstance is IMobilityModifyingItem item)
					cost *= item.StaminaUsageMultiplier;

				if(ev.Player.ReferenceHub.fpc.staminaController._scp1853.IsEnabled)
					cost *= ev.Player.ReferenceHub.fpc.staminaController._scp1853.CurStaminaMultiplier;

				if(!ev.Player.ReferenceHub.fpc.staminaController._scp207.IsEnabled && !ev.Player.ReferenceHub.fpc.staminaController._invigorated.IsEnabled)
				{
					ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina -= cost;
					ev.Player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;
				}
			}
		}
		public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
		{
			Log.Debug($"[OnSpawningRagdoll] {ev.Owner.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			ev.Info = new RagdollInfo(ev.Info.OwnerHub, ev.Info.Handler, ev.Info.RoleType, ev.Info.StartPosition, ev.Info.StartRotation, ev.Info.Nickname, ev.Info.CreationTime + plugin.Config.Scp049AddAllowrecallTime);

			//死体削除
			if(ev.DamageHandlerBase is UniversalDamageHandler universal)
				if(plugin.Config.PocketdimensionClean && universal.TranslationId == DeathTranslations.PocketDecay.Id
					|| plugin.Config.TeslaDeleteObjects && universal.TranslationId == DeathTranslations.Tesla.Id)
					ev.IsAllowed = false;

			if(ev.DamageHandlerBase is ScpDamageHandler scp && scp._translationId == DeathTranslations.PocketDecay.Id)
				ev.IsAllowed = false;
			if(ev.DamageHandlerBase is Scp096DamageHandler)
				ev.IsAllowed = false;
		}
		public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
		{
			Log.Debug($"[OnFailingEscapePocketDimension] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ポケディメデス時SCP-106へ経験値
			if(plugin.Config.DataEnabled)
				foreach(var player in Player.Get(RoleType.Scp106))
					if(SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.ContainsKey(player.UserId))
						SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict[player.UserId].AddExp(plugin.Config.LevelExpKill);


			foreach(var player in Player.Get(RoleType.Scp106))
			{
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.BigHitmarker(player, 2f), Segment.FixedUpdate));
				if(!RoundSummary.singleton.RoundEnded) KillsDict[player.Nickname] += 1;
			}
		}
		public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
		{
			//テスラを無効にするチーム
			if(plugin.Config.TeslaDisabledTeamsParsed.Contains(ev.Player.Role.Team))
			{
				ev.IsInIdleRange = false;
				ev.IsTriggerable = false;
			}
		}
		public void OnProcessingHotkey(ProcessingHotkeyEventArgs ev)
		{
			Log.Debug($"[OnProcessingHotkey] {ev.Player.Nickname} -> {ev.Hotkey}", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-Hotkeys
			if((plugin.Config.Scp106ExHotkey && ev.Player.Role == RoleType.Scp106) || (plugin.Config.Scp079ExHotkey && ev.Player.Role == RoleType.Scp079))
			{
				ev.IsAllowed = false;
				if(SanyaPluginComponent.Instances.TryGetValue(ev.Player, out var component))
					component.OnProcessingHotkey(ev.Hotkey);
			} 
		}

		//Scp106
		public void OnContaining(ContainingEventArgs ev)
		{
			Door.Get(Exiled.API.Enums.DoorType.Scp106Primary).Base.NetworkTargetState = true;
			Door.Get(Exiled.API.Enums.DoorType.Scp106Primary).Base.ServerChangeLock(DoorLockReason.DecontEvacuate, true);
			Door.Get(Exiled.API.Enums.DoorType.Scp106Secondary).Base.NetworkTargetState = true;
			Door.Get(Exiled.API.Enums.DoorType.Scp106Secondary).Base.ServerChangeLock(DoorLockReason.DecontEvacuate, true);
			Door.Get(Exiled.API.Enums.DoorType.Scp106Bottom).Base.NetworkTargetState = true;
			Door.Get(Exiled.API.Enums.DoorType.Scp106Bottom).Base.ServerChangeLock(DoorLockReason.DecontEvacuate, true);
		}
	}
}