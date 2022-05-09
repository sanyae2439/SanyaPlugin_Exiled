using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using Exiled.API.Enums;
using Exiled.API.Features;
using InventorySystem;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using Mirror;
using RemoteAdmin;
using Respawning;
using UnityEngine;

namespace SanyaPlugin
{
	public static class Methods
	{
		public static HttpClient httpClient = new HttpClient();

		public static string ToStringPropertiesAndFields(object instance)
		{
			string returned = "\n";

			foreach(PropertyInfo info in instance.GetType().GetProperties())
				if(info.PropertyType.IsList())
				{
					returned += $"{info.Name}:\n";
					if(info.GetValue(instance) is IEnumerable list)
						foreach(var i in list) returned += $"{i}\n";
				}
				else if(info.PropertyType.IsDictionary())
				{
					returned += $"{info.Name}: ";

					var obj = info.GetValue(instance);

					IDictionary dict = (IDictionary)obj;

					var key = obj.GetType().GetProperty("Keys");
					var value = obj.GetType().GetProperty("Values");
					var keyObj = key.GetValue(obj, null);
					var valueObj = value.GetValue(obj, null);
					var keyEnum = keyObj as IEnumerable;

					foreach(var i in dict.Keys)
						returned += $"[{i}:{dict[i]}]";

					returned += "\n";
				}
				else
					returned += $"{info.Name}: {info.GetValue(instance)}\n";

			foreach(FieldInfo info in instance.GetType().GetFields())
				if(info.FieldType.IsList())
				{
					returned += $"{info.Name}:\n";
					if(info.GetValue(instance) is IEnumerable list)
						foreach(var i in list) returned += $"{i}\n";
				}
				else if(info.FieldType.IsDictionary())
				{
					returned += $"{info.Name}: ";

					var obj = info.GetValue(instance);

					IDictionary dict = (IDictionary)obj;

					var key = obj.GetType().GetProperty("Keys");
					var value = obj.GetType().GetProperty("Values");
					var keyObj = key.GetValue(obj, null);
					var valueObj = value.GetValue(obj, null);
					var keyEnum = keyObj as IEnumerable;

					foreach(var i in dict.Keys)
						if(dict[i].GetType().IsList())
						{
							returned += $"[{i}:";
							if(dict[i] is IEnumerable list)
								foreach(var x in list) returned += $"{x},";
							returned += "]";
						}
						else
							returned += $"[{i}:{dict[i]}]";

					returned += "\n";
				}
				else
					returned += $"{info.Name}: {info.GetValue(instance)}\n";

			return returned;
		}

		public static void SpawnGrenade(Vector3 position, ItemType id, float fusedur = -1, ReferenceHub player = null)
		{
			if(!InventoryItemLoader.AvailableItems.TryGetValue(id, out var itemBase) || !(itemBase is ThrowableItem throwableItem))
				return;

			ThrownProjectile thrownProjectile = UnityEngine.Object.Instantiate(throwableItem.Projectile);
			TimeGrenade timeGrenade = thrownProjectile as TimeGrenade;

			if(thrownProjectile.TryGetComponent<Rigidbody>(out var rigidbody))
				rigidbody.position = position;

			thrownProjectile.PreviousOwner = new Footprinting.Footprint(player ?? ReferenceHub.HostHub);

			if(fusedur != -1)
				timeGrenade._fuseTime = fusedur;

			NetworkServer.Spawn(thrownProjectile.gameObject);

			thrownProjectile.ServerActivate();
		}

		public static ItemPickupBase SpawnItem(ItemType itemType, Vector3 position)
		{
			if(InventoryItemLoader.AvailableItems.TryGetValue(itemType, out var itemBase))
			{
				var itemPickUpBase = UnityEngine.Object.Instantiate(itemBase.PickupDropModel, position, Quaternion.identity);
				itemPickUpBase.Info.ItemId = itemType;
				itemPickUpBase.Info.Weight = itemBase.Weight;
				NetworkServer.Spawn(itemPickUpBase.gameObject);
				var info = new InventorySystem.Items.Pickups.PickupSyncInfo()
				{
					ItemId = itemType,
					Serial = InventorySystem.Items.ItemSerialGenerator.GenerateNext(),
					Weight = itemBase.Weight,
					Position = position,
					Rotation = new LowPrecisionQuaternion(Quaternion.identity),
					Locked = false
				};
				itemPickUpBase.NetworkInfo = info;
				return itemPickUpBase;
			}
			return null;
		}

		public static int GetRandomIndexFromWeight(int[] list)
		{
			int sum = 0;

			foreach(int i in list)
			{
				if(i <= 0) continue;
				sum += i;
			}

			int random = UnityEngine.Random.Range(0, sum);
			for(int i = 0; i < list.Length; i++)
			{
				if(list[i] <= 0) continue;

				if(random < list[i])
				{
					return i;
				}
				random -= list[i];
			}
			return -1;
		}

		public static void SendSubtitle(string text, ushort time, Player target = null)
		{
			if(target != null)
			{
				target.ClearBroadcasts();
				target.Broadcast(time, text, Broadcast.BroadcastFlags.Normal, false);
			}
			else
			{
				Map.ClearBroadcasts();
				Map.Broadcast(time, text, Broadcast.BroadcastFlags.Normal, false);
			}
		}

		public static void PlayAmbientSound(int id) => PlayerManager.localPlayer.GetComponent<AmbientSoundPlayer>().RpcPlaySound(Mathf.Clamp(id, 0, 31));

		public static void PlayRandomAmbient() => PlayAmbientSound(UnityEngine.Random.Range(0, 32));

		public static string FormatServerName()
		{
			string result = ServerConsole.singleton.RefreshServerName();
			result = Regex.Replace(result, @"SM119.\d+.\d+.\d+ \(EXILED\)", string.Empty);
			result = Regex.Replace(result, @"\[.+?\]", string.Empty);
			result = Regex.Replace(result, @"\<.+?\>", string.Empty);
			return result.Trim();
		}

		public static bool CanLookToPlayer(this Camera079 camera, Player player)
		{
			if(player.Role == RoleType.Spectator || player.Role == RoleType.Scp079 || player.Role == RoleType.None)
				return false;

			float num = Vector3.Dot(camera.head.transform.forward, player.Position - camera.transform.position);

			return (num >= 0f && num * num / (player.Position - camera.transform.position).sqrMagnitude > 0.4225f)
				&& Physics.Raycast(camera.transform.position, player.Position - camera.transform.position, out RaycastHit raycastHit, 100f, -117407543)
				&& raycastHit.transform.name == player.GameObject.name;
		}

		public static GameObject SpawnDummy(RoleType role, Vector3 pos, Quaternion rot)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.gameObject.name == "Player"));
			CharacterClassManager ccm = gameObject.GetComponent<CharacterClassManager>();
			ccm.CurClass = role;
			ccm.RefreshPlyModel();
			gameObject.GetComponent<NicknameSync>().Network_myNickSync = "Dummy";
			gameObject.GetComponent<QueryProcessor>().NetworkPlayerId = 9999;
			gameObject.transform.position = pos;
			gameObject.transform.rotation = rot;
			NetworkServer.Spawn(gameObject);
			return gameObject;
		}

		public static int GetMTFTickets()
		{
			if(CustomLiteNetLib4MirrorTransport.DelayConnections) return -1;
			return RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox);
		}

		public static int GetCITickets()
		{
			if(CustomLiteNetLib4MirrorTransport.DelayConnections) return -1;
			return RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency);
		}

		public static bool IsStuck(Vector3 pos)
		{
			bool result = false;
			foreach(Collider collider in Physics.OverlapBox(pos, new Vector3(0.4f, 1f, 0.4f), new Quaternion(0f, 0f, 0f, 0f)))
			{
				bool flag = collider.name.Contains("Hitbox") || collider.name.Contains("mixamorig") || collider.name.Equals("Player") || collider.name.Equals("PlyCenter") || collider.name.Equals("Antijumper");
				if(!flag)
				{
					Log.Debug($"Detect:{collider.name}", SanyaPlugin.Instance.Config.IsDebugged);
					result = true;
				}
			}
			return result;
		}

		public static void MoveNetworkIdentityObject(NetworkIdentity identity, Vector3 pos)
		{
			identity.gameObject.transform.position = pos;
			ObjectDestroyMessage objectDestroyMessage = new ObjectDestroyMessage();
			objectDestroyMessage.netId = identity.netId;
			foreach(var ply in Player.List)
			{
				ply.Connection.Send(objectDestroyMessage, 0);
				typeof(NetworkServer).GetMethod("SendSpawnMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { identity, ply.Connection });
			}
		}

		public static NetworkIdentity GetSinkHoleHazard()
		{
			foreach(var identity in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
				if(identity.name == "Sinkhole")
					return identity;
			return null;
		}

		public static bool IsAlphaWarheadCountdown()
		{
			return AlphaWarheadController.Host.timeToDetonation <
				AlphaWarheadController.Host.RealDetonationTime() -
				((AlphaWarheadController._resumeScenario >= 0)
				? AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].additionalTime
				: AlphaWarheadController.Host.scenarios_start[AlphaWarheadController._startScenario].additionalTime);
		}

		public static float GetCurrentTimeToDetonationTime() => AlphaWarheadController._resumeScenario == -1
				? AlphaWarheadController.Host.scenarios_start[AlphaWarheadController._startScenario].tMinusTime
				: AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].tMinusTime;

		public static float GetAdditionalTime() => AlphaWarheadController._resumeScenario == -1
				? AlphaWarheadController.Host.scenarios_start[AlphaWarheadController._startScenario].additionalTime
				: AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].additionalTime;

		public static void SetAmmoConfigs()
		{
			foreach(var role in CharacterClassManager._staticClasses.Where(x => x.team != Team.SCP && x.team != Team.RIP))
				if(SanyaPlugin.Instance.Config.DefaultammosParsed.TryGetValue(role.roleId, out var value2))
				{
					if(!InventorySystem.Configs.StartingInventories.DefinedInventories.TryGetValue(role.roleId, out var value))
						InventorySystem.Configs.StartingInventories.DefinedInventories.Add(role.roleId, new InventorySystem.InventoryRoleInfo(new ItemType[] { }, new Dictionary<ItemType, ushort>()));
					else
						value.Ammo.Clear();

					foreach(var ammo in value2)
						InventorySystem.Configs.StartingInventories.DefinedInventories[role.roleId].Ammo[ammo.Key] = ammo.Value;
				}
		}

		public static string TranslateZoneName(ZoneType zone)
		{
			switch(zone)
			{
				case ZoneType.Surface:
					return "地上";
				case ZoneType.Entrance:
					return "エントランス";
				case ZoneType.HeavyContainment:
					return "重度収容区画";
				case ZoneType.LightContainment:
					return "軽度収容区画";
				case ZoneType.Unspecified:
					return "不明";
				default:
					return "エラー";
			}
		}

		public static string TranslateZoneNameForShort(ZoneType zone)
		{
			switch(zone)
			{
				case ZoneType.Surface:
					return "地上";
				case ZoneType.Entrance:
					return "EZ";
				case ZoneType.HeavyContainment:
					return "HCZ";
				case ZoneType.LightContainment:
					return "LCZ";
				case ZoneType.Unspecified:
					return "不明";
				default:
					return "エラー";
			}
		}

		public static string TranslateRoomName(RoomType room)
		{
			switch(room)
			{
				case RoomType.Unknown:
					return "不明";
				case RoomType.LczArmory:
					return "武器庫";
				case RoomType.LczCurve:
					return "曲がり角";
				case RoomType.LczStraight:
					return "直線通路";
				case RoomType.Lcz012:
					return "SCP-012収容室";
				case RoomType.Lcz914:
					return "SCP-914収容室";
				case RoomType.LczCrossing:
					return "交差点";
				case RoomType.LczTCross:
					return "三叉路";
				case RoomType.LczCafe:
					return "PCルーム";
				case RoomType.LczPlants:
					return "栽培室";
				case RoomType.LczToilets:
					return "トイレ";
				case RoomType.LczAirlock:
					return "エアロック";
				case RoomType.Lcz173:
					return "SCP-173収容室";
				case RoomType.LczClassDSpawn:
					return "Dクラス職員収容室";
				case RoomType.LczChkpB:
					return "チェックポイントB-L";
				case RoomType.LczGlassBox:
					return "SCP-372収容室";
				case RoomType.LczChkpA:
					return "チェックポイントA-L";
				case RoomType.Lcz330:
					return "SCP-330テストチェンバー";
				case RoomType.Hcz079:
					return "SCP-079収容室";
				case RoomType.HczEzCheckpoint:
					return "チェックポイントE-H";
				case RoomType.HczArmory:
					return "武器庫";
				case RoomType.Hcz939:
					return "テストルーム";
				case RoomType.HczHid:
					return "MicroHID格納庫";
				case RoomType.Hcz049:
					return "SCP-049収容室";
				case RoomType.HczChkpA:
					return "チェックポイントA-H";
				case RoomType.HczCrossing:
					return "交差点";
				case RoomType.Hcz106:
					return "SCP-106収容室";
				case RoomType.HczNuke:
					return "AlphaWarhead格納庫";
				case RoomType.HczTesla:
					return "テスラゲート"; ;
				case RoomType.HczServers:
					return "サーバールーム";
				case RoomType.HczChkpB:
					return "チェックポントB-H";
				case RoomType.HczTCross:
					return "三叉路";
				case RoomType.HczCurve:
					return "曲がり角";
				case RoomType.Hcz096:
					return "SCP-096収容室";
				case RoomType.EzVent:
					return "搬出ゲート";
				case RoomType.EzIntercom:
					return "放送室";
				case RoomType.EzGateA:
					return "ゲートA";
				case RoomType.EzDownstairsPcs:
					return "通路横PCルーム";
				case RoomType.EzCurve:
					return "曲がり角";
				case RoomType.EzPcs:
					return "PCルーム";
				case RoomType.EzCrossing:
					return "三叉路";
				case RoomType.EzCollapsedTunnel:
					return "崩壊した通路";
				case RoomType.EzConference:
					return "VIPルーム";
				case RoomType.EzStraight:
					return "直線通路";
				case RoomType.EzCafeteria:
					return "ベンチ付き直線通路";
				case RoomType.EzUpstairsPcs:
					return "2階付きPCルーム";
				case RoomType.EzGateB:
					return "ゲートB";
				case RoomType.EzShelter:
					return "非常用シェルター";
				case RoomType.Pocket:
					return "[削除済み]";
				case RoomType.Surface:
					return "地上";
				case RoomType.HczStraight:
					return "直線通路";
				case RoomType.EzTCross:
					return "三叉路";
				default:
					return "エラー";
			}
		}
	}
}
