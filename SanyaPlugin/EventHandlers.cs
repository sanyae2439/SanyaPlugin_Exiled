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
using InventorySystem.Items.Armor;
using InventorySystem.Items.Firearms.Ammo;
using InventorySystem.Items.Keycards;
using InventorySystem.Items.Usables.Scp244;
using LightContainmentZoneDecontamination;
using LiteNetLib.Utils;
using MapGeneration;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using PlayerStatsSystem;
using Respawning;
using Scp914.Processors;
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
		public readonly static Dictionary<string, bool> EscapedClassDDict = new Dictionary<string, bool>();
		public readonly static Dictionary<string, bool> EscapedScientistDict = new Dictionary<string, bool>();
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedDamages;
		public static IOrderedEnumerable<KeyValuePair<string, uint>> sortedKills;
		private Vector3 nextRespawnPos = Vector3.zero;
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

			//プレイヤーデータの初期化
			SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.Clear();

			//初期スポーンデータのロード(Fix maingame 11.x)
			SpawnpointManager.FillSpawnPoints();

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
			foreach(var i in RoomIdentifier.AllRoomIdentifiers.Where(x => x.Zone != FacilityZone.Surface))
				if(UnityEngine.Random.Range(0, 100) < plugin.Config.Scp244SpawnPercent)
					(Methods.SpawnItem(UnityEngine.Random.Range(0, 2) == 0 ? ItemType.SCP244a : ItemType.SCP244b, i.transform.position) as Scp244DeployablePickup).State = Scp244State.Active;

			//地上脱出口の二つのドアとHIDのドアにグレネード耐性をつける
			(DoorNametagExtension.NamedDoors["HID"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_PRIMARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;
			(DoorNametagExtension.NamedDoors["ESCAPE_SECONDARY"].TargetDoor as BreakableDoor)._ignoredDamageSources |= DoorDamageType.Grenade;

			//AlphaWarheadの設定
			if(plugin.Config.AlphaWarheadLockAlways)
			{
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

				//Prefabの確保
				var primitivePrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("Primitive"));
				var lightPrefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("LightSource"));
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

				//的の設置
				var target1 = UnityEngine.Object.Instantiate(sportPrefab, new Vector3(-24.5f, 1000f, -68f), Quaternion.Euler(Vector3.up * 180f));
				var target2 = UnityEngine.Object.Instantiate(sportPrefab, new Vector3(-24.5f, 1000f, -72.5f), Quaternion.Euler(Vector3.up * 180f));
				var target3 = UnityEngine.Object.Instantiate(dboyPrefab, new Vector3(-24.5f, 1000f, -70.25f), Quaternion.Euler(Vector3.up * 180f));

				//核起爆室のライト
				var light_nuke = UnityEngine.Object.Instantiate(lightPrefab.GetComponent<LightSourceToy>());
				light_nuke.transform.position = new Vector3(40.75f, 991f, -35.75f);
				light_nuke.NetworkLightRange = 4.5f;
				light_nuke.NetworkLightIntensity = 2f;
				light_nuke.NetworkLightColor = new Color(1f, 0f, 0f);

				//核起動室上箱
				var wall_fence1 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_fence1.transform.position = new Vector3(50f, 1002f, -57f);
				wall_fence1.transform.localScale = new Vector3(0.5f, 5f, 19.7f);
				wall_fence1.UpdatePositionServer();
				wall_fence1.NetworkMaterialColor = Color.white;
				wall_fence1.NetworkPrimitiveType = PrimitiveType.Cube;

				var wall_fence2 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_fence2.transform.position = new Vector3(53.5f, 1002f, -66.61f);
				wall_fence2.transform.localScale = new Vector3(7f, 5f, 0.5f);
				wall_fence2.UpdatePositionServer();
				wall_fence2.NetworkMaterialColor = Color.white;
				wall_fence2.NetworkPrimitiveType = PrimitiveType.Cube;

				var wall_fence3 = UnityEngine.Object.Instantiate(primitivePrefab.GetComponent<PrimitiveObjectToy>());
				wall_fence3.transform.position = new Vector3(52.5f, 1004.24f, -57.59f);
				wall_fence3.transform.localScale = new Vector3(5f, 0.5f, 18.5f);
				wall_fence3.UpdatePositionServer();
				wall_fence3.NetworkMaterialColor = Color.white;
				wall_fence3.NetworkPrimitiveType = PrimitiveType.Cube;

				//SCP-106のコンテナ壁
				var room106 = Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.Hcz106);
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
				var room939 = Map.Rooms.First(x => x.Type == Exiled.API.Enums.RoomType.Hcz939);
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
				NetworkServer.Spawn(target1);
				NetworkServer.Spawn(target2);
				NetworkServer.Spawn(target3);
				NetworkServer.Spawn(light_nuke.gameObject);
				NetworkServer.Spawn(wall_fence1.gameObject);
				NetworkServer.Spawn(wall_fence2.gameObject);
				NetworkServer.Spawn(wall_fence3.gameObject);
				NetworkServer.Spawn(wall_106_1.gameObject);
				NetworkServer.Spawn(wall_106_2.gameObject);
				NetworkServer.Spawn(wall_939_1.gameObject);
			}

			//アイテム追加
			if(plugin.Config.AddItemsOnFacility)
			{
				var hczradio = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.Radio && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position + Vector3.up * 2;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;
				(Methods.SpawnItem(ItemType.Ammo762x39, hczradio) as AmmoPickup).NetworkSavedAmmo = 30;

				var hczcom18 = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.GunCOM18 && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position + Vector3.up * 2;
				Methods.SpawnItem(ItemType.KeycardNTFOfficer, hczcom18);

				var hczflash = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.Flashlight && x.TriggerDoorName == "HCZ_ARMORY")._positionVariants.First().position + Vector3.up * 2;
				Methods.SpawnItem(ItemType.Adrenaline, hczflash);
				Methods.SpawnItem(ItemType.Medkit, hczflash);

				var lczfsp9 = ItemSpawnpoint.AutospawnInstances.First(x => x.AutospawnItem == ItemType.GunFSP9 && x.TriggerDoorName == "LCZ_ARMORY")._positionVariants.First().position + Vector3.up * 2;
				Methods.SpawnItem(ItemType.KeycardNTFOfficer, lczfsp9);
				Methods.SpawnItem(ItemType.Medkit, lczfsp9);
				Methods.SpawnItem(ItemType.Medkit, lczfsp9);
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

			var surfaceLight = UnityEngine.Object.FindObjectsOfType<RoomIdentifier>().First(x => x.Zone == FacilityZone.Surface).GetComponentInChildren<FlickerableLightController>();
			surfaceLight.Network_warheadLightOverride = true;
			surfaceLight.Network_warheadLightColor = new Color(plugin.Config.LightColorSurface.r / 255f, plugin.Config.LightColorSurface.g / 255f, plugin.Config.LightColorSurface.b / 255f);


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

			if(plugin.Config.ScpRoomLockWhenSafe)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.CheckScpsRoom(), Segment.FixedUpdate));

			if(plugin.Config.ClassdPrisonInit)
				roundCoroutines.Add(Timing.RunCoroutine(Coroutines.InitClassDPrison(), Segment.FixedUpdate));

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
		public void OnEndingRound(EndingRoundEventArgs ev)
		{
			if(plugin.Config.AlphaWarheadEndRound && Warhead.IsDetonated && !ev.IsRoundEnded)
			{
				int scientist = ev.ClassList.scientists + RoundSummary.EscapedScientists;
				int classd = ev.ClassList.class_ds + RoundSummary.EscapedClassD;
				int scps = ev.ClassList.scps_except_zombies;

				if(scientist > 0 && scps == 0)
					ev.LeadingTeam = Exiled.API.Enums.LeadingTeam.FacilityForces;
				else if(classd > 0)
					ev.LeadingTeam = Exiled.API.Enums.LeadingTeam.ChaosInsurgency;
				else if(scientist == 0 && classd == 0 && scps > 0)
					ev.LeadingTeam = Exiled.API.Enums.LeadingTeam.Anomalies;
				else
					ev.LeadingTeam = Exiled.API.Enums.LeadingTeam.Draw;

				ev.IsRoundEnded = true;

				Log.Info($"[OnEndingRound] Force Ended by AlphaWarhead. Leading:{ev.LeadingTeam} Scientist:{scientist} ClassD:{classd} Scps:{scps}");
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
			Log.Info($"[OnRespawningTeam] Queues:{ev.Players.Count} IsCI:{ev.NextKnownTeam} MaxAmount:{ev.MaximumRespawnAmount}");

			//ラウンド終了後にリスポーンを停止する
			if(plugin.Config.GodmodeAfterEndround && !RoundSummary.RoundInProgress())
				ev.Players.Clear();

			//ランダムでリスポーン位置を変更する
			if(plugin.Config.RandomRespawnPosPercent > 0)
			{
				int randomnum = UnityEngine.Random.Range(0, 100);
				Log.Info($"[RandomRespawnPos] Check:{randomnum}<{plugin.Config.RandomRespawnPosPercent}");
				if(randomnum < plugin.Config.RandomRespawnPosPercent && !Warhead.IsDetonated && !Warhead.IsInProgress)
				{
					List<Vector3> poslist = new List<Vector3>();
					if(!Map.IsLczDecontaminated && DecontaminationController.Singleton._nextPhase < 3)
						poslist.Add(Map.GetCameraByType(Exiled.API.Enums.CameraType.Lcz173Hallway).transform.position + Vector3.down * 2);

					poslist.Add(RoleType.Scp93953.GetRandomSpawnProperties().Item1);
					poslist.Add(GameObject.FindGameObjectsWithTag("RoomID").First(x => x.GetComponent<Rid>()?.id == "Shelter").transform.position);

					foreach(var i in poslist)
						Log.Info($"[RandomRespawnPos] TargetLists:{i}");

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
		public void OnGeneratorActivated(GeneratorActivatedEventArgs ev)
		{
			Log.Debug($"[OnGeneratorActivated] {ev.Generator.GetComponentInParent<RoomIdentifier>()?.Name} ({Map.ActivatedGenerators + 1} / 3)", SanyaPlugin.Instance.Config.IsDebugged);

			//強制再収容のとき
			if(UnityEngine.Object.FindObjectOfType<Recontainer079>()._alreadyRecontained)
				return;

			if(plugin.Config.GeneratorFix)
				ev.Generator.ServerSetFlag(MapGeneration.Distributors.Scp079Generator.GeneratorFlags.Open, false);

			switch(eventmode)
			{
				case SANYA_GAME_MODE.BLACKOUT:
					{
						if(Map.ActivatedGenerators == 1)
						{
							foreach(var i in FlickerableLightController.Instances.Where(x => x.transform.root?.name != "Outside"))
							{
								i.WarheadLightColor = FlickerableLightController.DefaultWarheadColor;
								i.WarheadLightOverride = false;
							}	
						}

						break;
					}
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

			//サーバー以外が止められないようにする
			if(plugin.Config.AlphaWarheadLockAlways && !ev.Player.IsHost)
				ev.IsAllowed = false;
		}
		public void OnChangingLeverStatus(ChangingLeverStatusEventArgs ev)
		{
			Log.Debug($"[OnChangingLeverStatus] {ev.Player.Nickname} {ev.CurrentState} -> {!ev.CurrentState}", SanyaPlugin.Instance.Config.IsDebugged);

			if(plugin.Config.AlphaWarheadLockAlways && ev.CurrentState)
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
			if(plugin.Config.DataEnabled && plugin.Config.LevelEnabled
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
					ev.Player.EnableEffect<Deafened>();
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
				}
			}

			//SCP
			if(plugin.Config.ScpGift != ItemType.None && ev.NewRole != RoleType.Scp0492 && ev.NewRole.GetTeam() == Team.SCP)
			{
				ev.Items.Clear();
				for(int i = 0; i < 8; i++)
					ev.Items.Add(plugin.Config.ScpGift);
			}

			//Dクラスロールボーナス
			if(!string.IsNullOrEmpty(ev.Player.GroupName) && plugin.Config.ClassdBonusitemsForRoleParsed.TryGetValue(ev.Player.GroupName, out List<ItemType> bonusitems) && ev.NewRole == RoleType.ClassD)
				ev.Items.InsertRange(0, bonusitems);

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
			Log.Debug($"[OnHurting] {ev.Attacker?.Nickname}[{ev.Attacker?.Role}] -({ev.Amount})-> {ev.Target.Nickname}[{ev.Target.Role}]", SanyaPlugin.Instance.Config.IsDebugged);

			//SCP-049の治療中ダメージ
			if(ev.Attacker != ev.Target && ev.Target.Role == RoleType.Scp049 && ev.Target.CurrentScp is PlayableScps.Scp049 scp049 && scp049._recallInProgressServer)
			{
				ev.Amount *= plugin.Config.Scp049TakenDamageWhenCureMultiplier;
			}

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
							ev.Amount *= plugin.Config.RevolverDamageMultiplier;
						if(firearm.WeaponType == ItemType.GunShotgun)
							ev.Amount *= plugin.Config.ShotgunDamageMultiplier;
						break;
					}
				case MicroHidDamageHandler microHid:
					{
						ev.Amount *= plugin.Config.MicrohidDamageMultiplier;
						break;
					}
			}

			//ヘビィアーマーの効果値
			if(ev.Target.IsHuman() 
				&& (ev.Handler.Base is FirearmDamageHandler || ev.Handler.Base is ExplosionDamageHandler || ev.Handler.Base is Scp018DamageHandler)
				&& ev.Target.ReferenceHub.inventory.TryGetBodyArmor(out var bodyArmor) 
				&& bodyArmor.ItemTypeId == ItemType.ArmorHeavy)
			{
				ev.Amount *= plugin.Config.HeavyArmorDamageEfficacy;
			}

			//SCPのダメージ
			if(ev.Attacker != ev.Target && ev.Target.IsScp)
			{
				if(plugin.Config.ScpTakenDamageMultiplierParsed.TryGetValue(ev.Target.Role, out var value))
					ev.Amount *= value;
			}

			//こんぽーねんと
			if(SanyaPluginComponent.Instances.TryGetValue(ev.Target, out var component))
				component.OnDamage();

			//ダメージランキング
			if(!RoundSummary.singleton.RoundEnded && ev.Attacker != null && ev.Attacker.IsEnemy(ev.Target.Team) && ev.Attacker.IsHuman && ev.Amount > 0f)
				DamagesDict[ev.Attacker.Nickname] += (uint)ev.Amount;
		}
		public void OnDying(DyingEventArgs ev)
		{
			if(ev.Target.Role == RoleType.Spectator || ev.Target.Role == RoleType.None || ev.Target.IsGodModeEnabled || ev.Target.ReferenceHub.characterClassManager.SpawnProtected) return;
			Log.Debug($"[OnDying] {ev.Killer?.Nickname}[{ev.Killer?.Role}] -> {ev.Target.Nickname}[{ev.Target.ReferenceHub.characterClassManager._prevId}]", SanyaPlugin.Instance.Config.IsDebugged);

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
			if(!RoundSummary.singleton.RoundEnded && ev.Killer != ev.Target && ev.Killer.IsEnemy(ev.Target.Team))
				KillsDict[ev.Killer.Nickname] += 1;
		}
		public void OnEscaping(EscapingEventArgs ev)
		{
			Log.Debug($"[OnEscaping] {ev.Player.Nickname} {ev.Player.Role} -> {ev.NewRole}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.Player.Role == RoleType.ClassD)
				EscapedClassDDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.ChaosConscript);
			else if(ev.Player.Role == RoleType.Scientist)
				EscapedScientistDict.Add(ev.Player.Nickname, ev.NewRole == RoleType.NtfSpecialist);
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
				ev.Target.ReferenceHub.playerStats.DealDamage(new RecontainmentDamageHandler(new Footprinting.Footprint(ev.Cuffer.ReferenceHub)));
			}
		}
		public void OnUsedItem(UsedItemEventArgs ev)
		{
			Log.Debug($"[OnUsedItem] {ev.Player.Nickname} / {ev.Item.Type}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.Item.Type == ItemType.SCP500)
			{
				ev.Player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerAddProcess(75f);
				ev.Player.ReferenceHub.fpc.ResetStamina();
				ev.Player.EnableEffect<Invigorated>(30f);
			}

			if(ev.Item.Type == ItemType.Adrenaline || ev.Item.Type == ItemType.Painkillers)
			{
				ev.Player.ReferenceHub.fpc.ModifyStamina(100f);
			}
		}
		public void OnJumping(JumpingEventArgs ev)
		{
			Log.Debug($"[OnJumping] {ev.Player.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			//ジャンプ時スタミナ消費
			if(SanyaPlugin.Instance.Config.StaminaCostJump > 0
				&& ev.Player.ReferenceHub.characterClassManager.IsHuman()
				&& !ev.Player.ReferenceHub.fpc.staminaController._invigorated.IsEnabled
				&& !ev.Player.ReferenceHub.fpc.staminaController._scp207.IsEnabled)
			{
				ev.Player.ReferenceHub.fpc.staminaController.RemainingStamina -= SanyaPlugin.Instance.Config.StaminaCostJump;
				ev.Player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;
			}
		}
		public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
		{
			Log.Debug($"[OnSpawningRagdoll] {ev.Owner.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);

			ev.Info = new RagdollInfo(ev.Info.OwnerHub, ev.Info.Handler, ev.Info.RoleType, ev.Info.StartPosition, ev.Info.StartRotation, ev.Info.Nickname, ev.Info.CreationTime + plugin.Config.Scp049AddAllowrecallTime);

			//死体削除
			if(ev.DamageHandlerBase is UniversalDamageHandler universal)
			{
				if(plugin.Config.PocketdimensionClean && universal.TranslationId == DeathTranslations.PocketDecay.Id
					|| plugin.Config.TeslaDeleteObjects && universal.TranslationId == DeathTranslations.Tesla.Id)
				{
					ev.IsAllowed = false;
				}
			}
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
			if(plugin.Config.TeslaDisabledPermission != "None"
				&& ev.Player.IsHuman()
				&& ev.Player.CurrentItem != null
				&& (ev.Player.CurrentItem.Base is KeycardItem keycardItem)
				&& keycardItem.Permissions.ToString().Contains(plugin.Config.TeslaDisabledPermission))
			{
				ev.IsTriggerable = false;
				ev.IsInIdleRange = false;
			}	
		}
		public void OnInteractingShootingTarget(InteractingShootingTargetEventArgs ev)
		{
			Log.Debug($"[OnInteractingShootingTarget] {ev.Player.Nickname} -> {ev.TargetButton}", SanyaPlugin.Instance.Config.IsDebugged);

			if(ev.TargetButton == Exiled.API.Enums.ShootingTargetButton.Remove || ev.TargetButton == Exiled.API.Enums.ShootingTargetButton.ToggleSync)
				ev.IsAllowed = false;
		}
		public void OnInteractingElevator(InteractingElevatorEventArgs ev)
		{
			Log.Debug($"[OnInteractingElevator] {ev.Player.Nickname} -> {ev.Lift.elevatorName}({ev.Status}) lock:{ev.Lift.Network_locked}", plugin.Config.IsDebugged);

			if(ev.Lift.Network_locked)
				ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>()?.AddHudCenterDownText("<color=#bbee00><size=25>このエレベーターはロックされています</color></size>", 3);
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
		public void OnInteractingScp330(InteractingScp330EventArgs ev)
		{
			Log.Debug($"[OnInteractingScp330] {ev.Player.Nickname} -> {ev.UsageCount}(Sever:{ev.ShouldSever})", SanyaPlugin.Instance.Config.IsDebugged);

			//Fix EXILED(4.x)
			if(ev.UsageCount >= 2)
				ev.ShouldSever = true;
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
				ev.Player.ReferenceHub.GetComponent<SanyaPluginComponent>()?.AddHudCenterDownText(SanyaPlugin.Instance.Translation.Error079NotEnoughTier, 3);
			}
		}
		public void OnLockingDown(LockingDownEventArgs ev)
		{
			Log.Debug($"[OnLockingDown] {ev.Player.Nickname} -> {ev.RoomGameObject.Name}", SanyaPlugin.Instance.Config.IsDebugged);

			bool isDestroyed = false;
			foreach(var i in Scp079Interactable.InteractablesByRoomId[ev.RoomGameObject.UniqueId].Where(x => x.type == Scp079Interactable.InteractableType.Door && x != null))
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

					ev.Player.ReferenceHub.playerStats.DealDamage(new UniversalDamageHandler(914914f, DeathTranslations.Tesla));
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