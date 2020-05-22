using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using EXILED;
using EXILED.Extensions;
using MEC;
using Mirror;
using RemoteAdmin;
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
			string targetuseridpath = Path.Combine(SanyaPlugin.DataPath, $"{userid}.txt");
			if(!Directory.Exists(SanyaPlugin.DataPath)) Directory.CreateDirectory(SanyaPlugin.DataPath);
			if(!File.Exists(targetuseridpath)) return new PlayerData(DateTime.Now, userid, true, 0, 0, 0);
			else return ParsePlayerData(targetuseridpath);
		}

		public static void SavePlayerData(PlayerData data)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.DataPath, $"{data.userid}.txt");

			if(!Directory.Exists(SanyaPlugin.DataPath)) Directory.CreateDirectory(SanyaPlugin.DataPath);

			string[] textdata = new string[] {
				data.lastUpdate.ToString("yyyy-MM-ddTHH:mm:sszzzz"),
				data.userid,
				data.limited.ToString(),
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
				int.Parse(text[3]),
				int.Parse(text[4]),
				int.Parse(text[5])
				);
		}

		public static void ResetLimitedFlag()
		{
			foreach(var file in Directory.GetFiles(SanyaPlugin.DataPath))
			{
				var data = LoadPlayerData(file.Replace(".txt", string.Empty));
				Log.Warn($"{data.userid}:{data.limited}");
				data.limited = true;
				SavePlayerData(data);
			}
		}
	}

	internal static class ShitChecker
	{
		private static string whitelist_path = Path.Combine(SanyaPlugin.DataPath, "VPN-Whitelist.txt");
		public static HashSet<IPAddress> whitelist = new HashSet<IPAddress>();
		private static string blacklist_path = Path.Combine(SanyaPlugin.DataPath, "VPN-Blacklist.txt");
		public static HashSet<IPAddress> blacklist = new HashSet<IPAddress>();

		public static IEnumerator<float> CheckVPN(PreauthEvent ev)
		{
			IPAddress address = ev.Request.RemoteEndPoint.Address;

			if(IsWhiteListed(address) || IsBlacklisted(address))
			{
				Log.Debug($"[VPNChecker] Already Checked:{address}");
				yield break;
			}

			using(UnityWebRequest unityWebRequest = UnityWebRequest.Get($"https://v2.api.iphub.info/ip/{address}"))
			{
				unityWebRequest.SetRequestHeader("X-Key", Configs.kick_vpn_apikey);
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
						Log.Info($"[VPNChecker] VPN Detected:{address} UserId:{ev.UserId}");
						AddBlacklist(address);

						ReferenceHub player = Player.GetPlayer(ev.UserId);
						if(player != null)
						{
							ServerConsole.Disconnect(player.characterClassManager.connectionToClient, Subtitles.VPNKickMessage);
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

		public static IEnumerator<float> CheckIsLimitedSteam(string userid)
		{
			PlayerData data = null;
			if(Configs.data_enabled && PlayerDataManager.playersData.TryGetValue(userid, out data) && !data.limited)
			{
				Log.Debug($"[SteamCheck] Already Checked:{userid}");
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
					while(xmlReader.Read())
					{
						if(xmlReader.ReadToFollowing("isLimitedAccount"))
						{
							string isLimited = xmlReader.ReadElementContentAsString();
							if(isLimited == "0")
							{
								Log.Info($"[SteamCheck] OK:{userid}");
								if(data != null)
								{
									data.limited = false;
									PlayerDataManager.SavePlayerData(data);
								}
								yield break;
							}
							else
							{
								Log.Warn($"[SteamCheck] NG:{userid}");
								ReferenceHub player = Player.GetPlayer(userid);
								if(player != null)
								{
									ServerConsole.Disconnect(player.characterClassManager.connectionToClient, Subtitles.LimitedKickMessage);
								}

								if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
									EventHandlers.kickedbyChecker.Add(userid, "steam");

								yield break;
							}
						}
						else
						{
							Log.Warn($"[SteamCheck] Falied(NoProfile):{userid}");
							ReferenceHub player = Player.GetPlayer(userid);
							if(player != null)
							{
								ServerConsole.Disconnect(player.characterClassManager.connectionToClient, Subtitles.NoProfileKickMessage);
							}
							if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
								EventHandlers.kickedbyChecker.Add(userid, "steam");
							yield break;
						}
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
			{
				if(IPAddress.TryParse(line, out var address))
				{
					whitelist.Add(address);
				}
			}

			foreach(var line2 in File.ReadAllLines(blacklist_path))
			{
				if(IPAddress.TryParse(line2, out var address2))
				{
					blacklist.Add(address2);
				}
			}
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
		public static bool isAirBombGoing = false;
		public static readonly Dictionary<ReferenceHub, CoroutineHandle> DOTDamages = new Dictionary<ReferenceHub, CoroutineHandle>();

		public static IEnumerator<float> GrantedLevel(ReferenceHub player, PlayerData data)
		{
			yield return Timing.WaitForSeconds(1f);

			var group = player.serverRoles.Group?.Clone();
			string level = data.level.ToString();
			string rolestr = player.serverRoles.GetUncoloredRoleString();
			string rolecolor = player.serverRoles.MyColor;
			string badge;

			rolestr = rolestr.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

			if(rolecolor == "light_red")
			{
				rolecolor = "pink";
			}

			if(data.level == -1)
			{
				level = "???";
			}

			if(string.IsNullOrEmpty(rolestr))
			{
				badge = $"Level{level}";
			}
			else
			{
				badge = $"Level{level} : {rolestr}";
			}

			if(Configs.disable_chat_bypass_whitelist && WhiteList.IsOnWhitelist(player.GetUserId()))
			{
				badge += " : 認証済み";
			}

			if(group == null)
			{
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
			}
			else
			{
				group.BadgeText = badge;
				group.BadgeColor = rolecolor;
				group.HiddenByDefault = false;
				group.Cover = true;
			}

			player.serverRoles.SetGroup(group, false, false, true);

			Log.Debug($"[GrantedLevel] {player.GetUserId()} : Level{level}");

			yield break;
		}

		public static IEnumerator<float> StartNightMode()
		{
			Log.Debug($"[StartNightMode] Started. Wait for {60}s...");
			yield return Timing.WaitForSeconds(60f);
			if(Configs.cassie_subtitle)
			{
				Methods.SendSubtitle(Subtitles.StartNightMode, 20);
			}
			PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("warning . facility power system has been attacked . all most containment zones light does not available until generator activated .", false, true);
			Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
			yield break;
		}

		public static IEnumerator<float> BigHitmark(MicroHID microHID)
		{
			yield return Timing.WaitForSeconds(0.1f);
			microHID.TargetSendHitmarker(false);
			yield break;
		}

		public static IEnumerator<float> AirSupportBomb(int waitforready = 5, int limit = -1)
		{
			Log.Info($"[AirSupportBomb] booting...");
			if(isAirBombGoing)
			{
				Log.Info($"[Airbomb] already booted, cancel.");
				yield break;
			}
			else
			{
				isAirBombGoing = true;
			}

			if(Configs.cassie_subtitle)
			{
				Methods.SendSubtitle(Subtitles.AirbombStarting, 10);
				PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("danger . outside zone emergency termination sequence activated .", false, true);
				yield return Timing.WaitForSeconds(5f);
			}

			Log.Info($"[AirSupportBomb] charging...");
			while(waitforready > 0)
			{
				Methods.PlayAmbientSound(7);
				waitforready--;
				yield return Timing.WaitForSeconds(1f);
			}

			Log.Info($"[AirSupportBomb] throwing...");
			int throwcount = 0;
			while(isAirBombGoing)
			{
				List<Vector3> randampos = OutsideRandomAirbombPos.Load().OrderBy(x => Guid.NewGuid()).ToList();
				foreach(var pos in randampos)
				{
					Methods.SpawnGrenade(pos, false, 0.1f);
					yield return Timing.WaitForSeconds(0.1f);
				}
				throwcount++;
				Log.Info($"[AirSupportBomb] throwcount:{throwcount}");
				if(limit != -1 && limit <= throwcount) 
				{
					isAirBombGoing = false;
					break; 
				}
				yield return Timing.WaitForSeconds(0.25f);
			}

			if(Configs.cassie_subtitle)
			{
				Methods.SendSubtitle(Subtitles.AirbombEnded, 10);
				PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("outside zone termination completed .", false, true);
			}

			Log.Info($"[AirSupportBomb] Ended.");
			yield break;
		}

		public static IEnumerator<float> DOTDamage(ReferenceHub target, int perDamage, int maxLimitDamage, float interval, DamageTypes.DamageType type)
		{
			int curDamageAmount = 0;
			Vector3 curDeathPos = target.characterClassManager.NetworkDeathPosition;
			RoleType curRole = target.GetRole();
			while(curDamageAmount < maxLimitDamage)
			{
				if(target.characterClassManager.NetworkDeathPosition != curDeathPos || target.GetRole() != curRole) break;
				target.playerStats.HurtPlayer(new PlayerStats.HitInfo(perDamage, "WORLD", type, 0), target.gameObject);
				curDamageAmount += perDamage;
				yield return Timing.WaitForSeconds(interval);
			}
			if(DOTDamages.ContainsKey(target))
			{
				Log.Debug($"[939DOT] Removed {target.GetNickname()}");
				DOTDamages.Remove(target);
			}
			yield break;
		}
	}

	internal static class Methods
	{
		public static HttpClient httpClient = new HttpClient();

		public static void SpawnGrenade(Vector3 position, bool isFlash = false, float fusedur = -1, ReferenceHub player = null)
		{
			if(player == null) player = ReferenceHub.GetHub(PlayerManager.localPlayer);
			var gm = player.GetComponent<Grenades.GrenadeManager>();
			Grenades.Grenade component = UnityEngine.Object.Instantiate(gm.availableGrenades[isFlash ? (int)GRENADE_ID.FLASH_NADE : (int)GRENADE_ID.FRAG_NADE].grenadeInstance).GetComponent<Grenades.Grenade>();
			if(fusedur != -1) component.fuseDuration = fusedur;
			component.FullInitData(gm, position, Quaternion.Euler(component.throwStartAngle), Vector3.zero, component.throwAngularVelocity);
			NetworkServer.Spawn(component.gameObject);
		}

		public static void Spawn018(ReferenceHub player)
		{
			var gm = player.GetComponent<Grenades.GrenadeManager>();
			var component = UnityEngine.Object.Instantiate(gm.availableGrenades[(int)GRENADE_ID.SCP018_NADE].grenadeInstance).GetComponent<Grenades.Scp018Grenade>();
			component.InitData(gm,
				new Vector3(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f)),
				new Vector3(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f)));
			NetworkServer.Spawn(component.gameObject);
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

		public static void SendSubtitle(string text, uint time, bool monospaced = false)
		{
			Broadcast brd = PlayerManager.localPlayer.GetComponent<Broadcast>();
			brd.RpcClearElements();
			brd.RpcAddElement(text, time, monospaced);
		}

		public static void TargetSendSubtitle(ReferenceHub player, string text, uint time, bool monospaced = false)
		{
			player.ClearBroadcasts();
			player.Broadcast(time, text, monospaced);
		}

		public static void PlayAmbientSound(int id)
		{
			PlayerManager.localPlayer.GetComponent<AmbientSoundPlayer>().RpcPlaySound(Mathf.Clamp(id, 0, 31));
		}

		public static void PlayRandomAmbient()
		{
			PlayAmbientSound(UnityEngine.Random.Range(0, 32));
		}

		public static void SendReport(ReferenceHub reported, string reason, ReferenceHub reporter)
		{
			var hookdata = new WebhookData();
			var embed = new Embed
			{
				title = "ゲームサーバーからの報告",
				timestamp = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffZ")
			};
			embed.footer.text = $"報告者:{reporter.GetNickname()} [{reporter.GetUserId()}]";
			embed.fields.Add(new EmbedField() { name = "発見サーバー", value = $"{FormatServerName()}" });
			embed.fields.Add(new EmbedField() { name = "対象プレイヤー名", value = $"{reported.GetNickname()}", inline = true });
			embed.fields.Add(new EmbedField() { name = "対象プレイヤーID", value = $"{reported.GetUserId()}", inline = true });
			embed.fields.Add(new EmbedField() { name = "内容", value = $"{reason}" });
			hookdata.embeds.Add(embed);

			var json = Utf8Json.JsonSerializer.ToJsonString<WebhookData>(hookdata);
			var data = new StringContent(json, Encoding.UTF8, "application/json");
			var result = httpClient.PostAsync(Configs.report_webhook, data).Result;

			Log.Debug($"{json}");

			if(result.IsSuccessStatusCode)
			{
				Log.Info($"[SendReport] Send Report.");
			}
			else
			{
				Log.Error($"[SendReport] Error. {result.StatusCode}");
			}
		}

		public static string FormatServerName()
		{
			string result = ServerConsole.singleton.RefreshServerName();
			result = Regex.Replace(result, @"SM119.\d+.\d+.\d+ \(EXILED\)", string.Empty);
			result = Regex.Replace(result, @"\[.+?\]", string.Empty);
			result = Regex.Replace(result, @"\<.+?\>", string.Empty);
			return result.Trim();
		}

		public static void Target096AttackSound(ReferenceHub target, ReferenceHub player)
		{
			NetworkWriter writer = NetworkWriterPool.GetWriter();
			player.TargetSendRpc(target.GetComponent<Scp096PlayerScript>(), "RpcSyncAudio", writer);
			NetworkWriterPool.Recycle(writer);
		}

		public static void TargetShake(this ReferenceHub target, bool achieve)
		{
			NetworkWriter writer = NetworkWriterPool.GetWriter();
			writer.WriteBoolean(achieve);
			target.TargetSendRpc(AlphaWarheadController.Host, nameof(AlphaWarheadController.RpcShake), writer);
			NetworkWriterPool.Recycle(writer);
		}

		public static void TargetSendRpc<T>(this ReferenceHub sendto, T target, string rpcName, NetworkWriter writer) where T : NetworkBehaviour
		{
			var msg = new RpcMessage
			{
				netId = target.netId,
				componentIndex = target.ComponentIndex,
				functionHash = target.GetType().FullName.GetStableHashCode() * 503 + rpcName.GetStableHashCode(),
				payload = writer.ToArraySegment()
			};
			sendto?.characterClassManager.connectionToClient.Send(msg, 0);
		}

		public static void SpawnRagdoll()
		{
			//UnityEngine.Object.FindObjectOfType<RagdollManager>().SpawnRagdoll(ev.Machine.output.position,
			//                                                   player.transform.rotation,
			//                                                   (int)player.GetRoleType(),
			//                                                   info,
			//                                                   false,
			//                                                   player.GetComponent<MirrorIgnorancePlayer>().PlayerId,
			//                                                   player.GetName(),
			//                                                   player.queryProcessor.PlayerId
			//                                                   );
		}

		public static bool CanLookToPlayer(this Camera079 camera, ReferenceHub player)
		{
			if(player.GetRole() == RoleType.Spectator || player.GetRole() == RoleType.Scp079 || player.GetRole() == RoleType.None)
				return false;

			Vector3 vector = player.transform.position - camera.transform.position;
			float num = Vector3.Dot(camera.head.transform.forward, vector);

			RaycastHit raycastHit;
			return (num >= 0f && num * num / vector.sqrMagnitude > 0.4225f)
				&& Physics.Raycast(camera.transform.position, vector, out raycastHit, 100f, -117407543)
				&& raycastHit.transform.name == player.name;
		}

		public static void Blink()
		{
			foreach(var scp173 in UnityEngine.Object.FindObjectsOfType<Scp173PlayerScript>())
			{
				scp173.RpcBlinkTime();
			}
		}

		public static void StartDecontEffectOnly(DecontaminationLCZ lcza)
		{
			lcza._curAnm = 10;
			foreach(var player in Player.GetHubs())
			{
				NetworkWriter writer = NetworkWriterPool.GetWriter();
				writer.WritePackedInt32(5);
				writer.WriteBoolean(true);
				player.TargetSendRpc(lcza, nameof(DecontaminationLCZ.RpcPlayAnnouncement), writer);
				NetworkWriterPool.Recycle(writer);
			}
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
	}

	internal static class Extensions
	{
		public static Task StartSender(this Task task)
		{
			return task.ContinueWith((x) => { Log.Error($"[Sender] {x}"); }, TaskContinuationOptions.OnlyOnFaulted);
		}

		public static bool IsEnemy(this ReferenceHub player, Team target)
		{
			if(player.GetRole() == RoleType.Spectator || player.GetRole() == RoleType.None || player.GetTeam() == target)
				return false;

			return target == Team.SCP ||
				((player.GetTeam() != Team.MTF && player.GetTeam() != Team.RSC) || (target != Team.MTF && target != Team.RSC))
				&&
				((player.GetTeam() != Team.CDP && player.GetTeam() != Team.CHI) || (target != Team.CDP && target != Team.CHI))
			;
		}

		public static void ShowHitmarker(this ReferenceHub player)
		{
			player.GetComponent<Scp173PlayerScript>().TargetHitMarker(player.characterClassManager.connectionToClient);
		}

		public static IEnumerable<Camera079> GetNearCams(this ReferenceHub player)
		{
			foreach(var cam in Scp079PlayerScript.allCameras)
			{
				var dis = Vector3.Distance(player.GetPosition(), cam.transform.position);
				if(dis <= 15f)
				{
					yield return cam;
				}
			}
		}

		public static T GetRandomOne<T>(this List<T> list)
		{
			return list[UnityEngine.Random.Range(0, list.Count)];
		}
	}
}