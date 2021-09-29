using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs;
using Hints;
using InventorySystem;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.ThrowableProjectiles;
using MEC;
using Mirror;
using RemoteAdmin;
using Respawning;
using SanyaPlugin.Data;
using UnityEngine;
using UnityEngine.Networking;
using Utf8Json;

namespace SanyaPlugin.Functions
{
	internal static class PlayerDataManager
	{
		public static Dictionary<string, PlayerData> playersData = new Dictionary<string, PlayerData>();

		public static PlayerData LoadPlayerData(string userid)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, $"{userid}.txt");
			if(!Directory.Exists(SanyaPlugin.Instance.Config.DataDirectory)) Directory.CreateDirectory(SanyaPlugin.Instance.Config.DataDirectory);
			if(!File.Exists(targetuseridpath)) return new PlayerData(DateTime.Now, userid, true, true, 0, 0, 0);
			else return ParsePlayerData(targetuseridpath);
		}

		public static void SavePlayerData(PlayerData data)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, $"{data.userid}.txt");

			if(!Directory.Exists(SanyaPlugin.Instance.Config.DataDirectory)) Directory.CreateDirectory(SanyaPlugin.Instance.Config.DataDirectory);

			string[] textdata = new string[] {
				data.lastUpdate.ToString("yyyy-MM-ddTHH:mm:sszzzz"),
				data.userid,
				data.steamlimited.ToString(),
				data.steamvacbanned.ToString(),
				data.level.ToString(),
				data.exp.ToString(),
				data.playingcount.ToString()
			};

			File.WriteAllLines(targetuseridpath, textdata);
		}

		private static PlayerData ParsePlayerData(string path)
		{
			var text = File.ReadAllLines(path);
			return new PlayerData(
				DateTime.Parse(text[0]),
				text[1],
				bool.Parse(text[2]),
				bool.Parse(text[3]),
				int.Parse(text[4]),
				int.Parse(text[5]),
				int.Parse(text[6])
				);
		}

		public static void ReloadParams()
		{
			foreach(var file in Directory.GetFiles(SanyaPlugin.Instance.Config.DataDirectory))
			{
				if(!file.Contains("@")) continue;
				var data = LoadPlayerData(file.Replace(".txt", string.Empty));
				data.steamlimited = true;
				data.steamvacbanned = true;
				SavePlayerData(data);
			}
		}
	}

	internal static class ShitChecker
	{
		private static string whitelist_path = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, "VPN-Whitelist.txt");
		public static HashSet<IPAddress> whitelist = new HashSet<IPAddress>();
		private static string blacklist_path = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, "VPN-Blacklist.txt");
		public static HashSet<IPAddress> blacklist = new HashSet<IPAddress>();

		public static IEnumerator<float> CheckVPN(PreAuthenticatingEventArgs ev)
		{
			IPAddress address = ev.Request.RemoteEndPoint.Address;

			if(IsWhiteListed(address) || IsBlacklisted(address))
			{
				Log.Debug($"[VPNChecker] Already Checked:{address}", SanyaPlugin.Instance.Config.IsDebugged);
				yield break;
			}

			using(UnityWebRequest unityWebRequest = UnityWebRequest.Get($"https://v2.api.iphub.info/ip/{address}"))
			{
				unityWebRequest.SetRequestHeader("X-Key", SanyaPlugin.Instance.Config.KickVpnApikey);
				yield return Timing.WaitUntilDone(unityWebRequest.SendWebRequest());
				if(!unityWebRequest.isNetworkError)
				{
					var data = JsonSerializer.Deserialize<VPNData>(unityWebRequest.downloadHandler.text);

					Log.Info($"[VPNChecker] Checking:{address}:{ev.UserId} ({data.countryCode}/{data.isp})");

					if(data.block == 0 || data.block == 2)
					{
						Log.Info($"[VPNChecker] Passed:{address} UserId:{ev.UserId}");
						AddWhitelist(address);
						yield break;
					}
					else if(data.block == 1)
					{
						Log.Warn($"[VPNChecker] VPN Detected:{address} UserId:{ev.UserId}");
						AddBlacklist(address);

						var player = Player.Get(ev.UserId);
						if(player != null)
						{
							ServerConsole.Disconnect(player.Connection, Subtitles.VPNKickMessage);
						}
						if(!EventHandlers.kickedbyChecker.ContainsKey(ev.UserId))
							EventHandlers.kickedbyChecker.Add(ev.UserId, "vpn");
						yield break;
					}
					else
					{
						Log.Error($"[VPNChecker] Error({unityWebRequest.responseCode}):block == {data.block}");
					}
				}
				else
				{
					Log.Error($"[VPNChecker] Error({unityWebRequest.responseCode}):{unityWebRequest.error}");
					yield break;
				}
			}
		}

		public static IEnumerator<float> CheckSteam(string userid)
		{
			PlayerData data = null;
			if(SanyaPlugin.Instance.Config.DataEnabled && PlayerDataManager.playersData.TryGetValue(userid, out data) 
				&& (!SanyaPlugin.Instance.Config.KickSteamLimited || !data.steamlimited)
				&& (!SanyaPlugin.Instance.Config.KickSteamVacBanned || !data.steamvacbanned))
			{
				Log.Debug($"[SteamCheck] Already Checked:{userid}", SanyaPlugin.Instance.Config.IsDebugged);
				yield break;
			}

			string xmlurl = string.Concat(
				"https://steamcommunity.com/profiles/",
				userid.Replace("@steam", string.Empty),
				"?xml=1"
			);
			using(UnityWebRequest unityWebRequest = UnityWebRequest.Get(xmlurl))
			{
				yield return Timing.WaitUntilDone(unityWebRequest.SendWebRequest());
				if(!unityWebRequest.isNetworkError)
				{
					XmlReaderSettings xmlReaderSettings = new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true };
					XmlReader xmlReader = XmlReader.Create(new MemoryStream(unityWebRequest.downloadHandler.data), xmlReaderSettings);
					bool ReadSuccess = false;
					while(xmlReader.Read())
					{
						if(xmlReader.ReadToFollowing("vacBanned") && SanyaPlugin.Instance.Config.KickSteamVacBanned)
						{
							ReadSuccess = true;
							string isVacBanned = xmlReader.ReadElementContentAsString();
							if(isVacBanned == "0")
							{
								Log.Info($"[SteamCheck:VacBanned] OK:{userid}");
								if(data != null)
								{
									data.steamvacbanned = false;
									PlayerDataManager.SavePlayerData(data);
								}
							}
							else
							{
								Log.Warn($"[SteamCheck:VacBanned] NG:{userid}");
								var player = Player.Get(userid);
								if(player != null)
									ServerConsole.Disconnect(player.Connection, Subtitles.VacBannedKickMessage);

								if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
									EventHandlers.kickedbyChecker.Add(userid, "steam_vac");
							}
						}

						if(xmlReader.ReadToFollowing("isLimitedAccount") && SanyaPlugin.Instance.Config.KickSteamLimited)
						{
							ReadSuccess = true;
							string isLimited = xmlReader.ReadElementContentAsString();
							if(isLimited == "0")
							{
								Log.Info($"[SteamCheck:Limited] OK:{userid}");
								if(data != null)
								{
									data.steamlimited = false;
									PlayerDataManager.SavePlayerData(data);
								}
							}
							else
							{
								Log.Warn($"[SteamCheck:Limited] NG:{userid}");
								var player = Player.Get(userid);
								if(player != null)
								{
									ServerConsole.Disconnect(player.Connection, Subtitles.LimitedKickMessage);
								}

								if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
									EventHandlers.kickedbyChecker.Add(userid, "steam_limited");
							}
						}


						if(!ReadSuccess)
						{
							Log.Warn($"[SteamCheck] Falied(NoProfile or Error):{userid}");
							var player = Player.Get(userid);
							if(player != null)
								ServerConsole.Disconnect(player.Connection, Subtitles.NoProfileKickMessage);
							if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
								EventHandlers.kickedbyChecker.Add(userid, "steam_noprofile");
						}

						yield break;
					}
				}
				else
				{
					Log.Error($"[SteamCheck] Failed(NetworkError):{userid}:{unityWebRequest.error}");
					yield break;
				}
			}
			yield break;
		}

		public static void LoadLists()
		{
			whitelist.Clear();
			blacklist.Clear();

			if(!File.Exists(whitelist_path))
				File.WriteAllText(whitelist_path, null);
			if(!File.Exists(blacklist_path))
				File.WriteAllText(blacklist_path, null);

			foreach(var line in File.ReadAllLines(whitelist_path))
				if(IPAddress.TryParse(line, out var address))
					whitelist.Add(address);

			foreach(var line2 in File.ReadAllLines(blacklist_path))
				if(IPAddress.TryParse(line2, out var address2))
					blacklist.Add(address2);
		}

		public static void AddWhitelist(IPAddress address)
		{
			whitelist.Add(address);
			using(StreamWriter writer = File.AppendText(whitelist_path))
			{
				writer.WriteLine(address);
			}
		}

		public static bool IsWhiteListed(IPAddress address)
		{
			return whitelist.Contains(address);
		}

		public static void AddBlacklist(IPAddress address)
		{
			blacklist.Add(address);
			using(StreamWriter writer = File.AppendText(blacklist_path))
			{
				writer.WriteLine(address);
			}
		}

		public static bool IsBlacklisted(IPAddress address)
		{
			return blacklist.Contains(address);
		}
	}

	internal static class Coroutines
	{
		public static IEnumerator<float> GrantedLevel(Player player, PlayerData data)
		{
			yield return Timing.WaitForSeconds(1f);

			if(player.GlobalBadge != null)
			{
				Log.Debug($"[GrantedLevel] User has GlobalBadge {player.UserId}:{player.GlobalBadge?.Text}", SanyaPlugin.Instance.Config.IsDebugged);
				yield break;
			}

			var group = player.Group?.Clone();
			string level = data.level.ToString();
			string rolestr = player.ReferenceHub.serverRoles.GetUncoloredRoleString();
			string rolecolor = player.RankColor;
			string badge = string.Empty;

			rolestr = rolestr.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

			if(rolecolor == "light_red")
				rolecolor = "pink";

			if(data.level == -1)
				level = "???";

			if(!player.DoNotTrack)
			{
				if(string.IsNullOrEmpty(rolestr))
					badge = $"Level{level}";
				else
					badge = $"Level{level} : {rolestr}";

				if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(player.UserId))
					badge += " : 認証済み";
			}
			else
			{
				if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(player.UserId))
				{
					if(!string.IsNullOrEmpty(rolestr))
						badge = $"{rolestr} : 認証済み";
					else
						badge = $"認証済み";
				}			
			}

			if(group == null)
				group = new UserGroup()
				{
					BadgeText = badge,
					BadgeColor = "default",
					HiddenByDefault = false,
					Cover = true,
					KickPower = 0,
					Permissions = 0,
					RequiredKickPower = 0,
					Shared = false
				};
			else
			{
				group.BadgeText = badge;
				group.BadgeColor = rolecolor;
				group.HiddenByDefault = false;
				group.Cover = true;
			}

			player.ReferenceHub.serverRoles.SetGroup(group, false, false, true);

			if(player.DoNotTrack)
				PlayerDataManager.playersData.Remove(player.UserId);

			Log.Debug($"[GrantedLevel] {player.UserId} : Level{level} : DNT={player.DoNotTrack}", SanyaPlugin.Instance.Config.IsDebugged);

			yield break;
		}

		public static IEnumerator<float> BigHitmarker(Player player, float size = 1f)
		{
			yield return Timing.WaitForSeconds(0.1f);
			player.SendHitmarker(size);
			yield break;
		}

		public static IEnumerator<float> InitBlackout()
		{
			yield return Timing.WaitForSeconds(10f);
			if(SanyaPlugin.Instance.Config.CassieSubtitle)
				Methods.SendSubtitle(EventTexts.BlackoutInit, 20);
			RespawnEffectsController.PlayCassieAnnouncement("warning . facility power system has been attacked . all most containment zones light does not available until generator activated .", false, true);
			foreach(var x in FlickerableLightController.Instances)
				x.ServerFlickerLights(5f);
			yield return Timing.WaitForSeconds(3f);
			foreach(var i in FlickerableLightController.Instances)
				i.LightIntensityMultiplier = 0f;
			yield break;
		}
	}

	internal static class Methods
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

		public static void PlayAmbientSound(int id)
		{
			PlayerManager.localPlayer.GetComponent<AmbientSoundPlayer>().RpcPlaySound(Mathf.Clamp(id, 0, 31));
		}

		public static void PlayRandomAmbient()
		{
			PlayAmbientSound(UnityEngine.Random.Range(0, 32));
		}

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
	}

	internal static class Extensions
	{
		public static Task StartSender(this Task task)
		{
			return task.ContinueWith((x) => { Log.Error($"[Sender] {x}"); }, TaskContinuationOptions.OnlyOnFaulted);
		}

		public static bool IsHuman(this Player player)
		{
			return player.Team != Team.SCP && player.Team != Team.RIP;
		}

		public static bool IsEnemy(this Player player, Team target)
		{
			if(player.Role == RoleType.Spectator || player.Role == RoleType.None || player.Team == target)
				return false;

			return target == Team.SCP ||
				((player.Team != Team.MTF && player.Team != Team.RSC) || (target != Team.MTF && target != Team.RSC))
				&&
				((player.Team != Team.CDP && player.Team != Team.CHI) || (target != Team.CDP && target != Team.CHI))
			;
		}

		public static int GetHealthAmountPercent(this Player player)
		{
			return (int)(100f - (player.ReferenceHub.playerStats.GetHealthPercent() * 100f));
		}

		public static int GetAHPAmountPercent(this Player player)
		{
			return (int)(100f - (Mathf.Clamp01(1f - player.ArtificialHealth / (float)player.MaxArtificialHealth) * 100f));
		}

		public static void SendTextHint(this Player player, string text, float time)
		{
			player.ReferenceHub.hints.Show(new TextHint(text, new HintParameter[] { new StringHintParameter(string.Empty) }, new HintEffect[] { HintEffectPresets.TrailingPulseAlpha(0.5f, 1f, 0.5f, 2f, 0f, 2) }, time));
		}

		public static void SendTextHintNotEffect(this Player player, string text, float time)
		{
			player.ReferenceHub.hints.Show(new TextHint(text, new HintParameter[] { new StringHintParameter(string.Empty) }, null, time));
		}

		public static IEnumerable<Camera079> GetNearCams(this Player player)
		{
			foreach(var cam in Scp079PlayerScript.allCameras)
			{
				var dis = Vector3.Distance(player.Position, cam.transform.position);
				if(dis <= 15f)
				{
					yield return cam;
				}
			}
		}

		public static void SendHitmarker(this Player player, float size = 1f) => Hitmarker.SendHitmarker(player.Connection, size);

		public static void SendReportText(this Player player, string text) => player.SendConsoleMessage($"[REPORTING] {text}", "white");

		public static bool IsList(this Type type)
		{
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
		}

		public static bool IsDictionary(this Type type)
		{
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
		}

		public static Type GetListArgs(this Type type)
		{
			return type.GetGenericArguments()[0];
		}

		public static T GetRandomOne<T>(this List<T> list)
		{
			return list[UnityEngine.Random.Range(0, list.Count)];
		}

		public static T Random<T>(this IEnumerable<T> ie)
		{
			if(!ie.Any()) return default;
			return ie.ElementAt(UnityEngine.Random.Range(0, ie.Count()));
		}
	}
}