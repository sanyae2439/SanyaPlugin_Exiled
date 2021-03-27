using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CustomPlayerEffects;
using Dissonance.Integrations.MirrorIgnorance;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs;
using Hints;
using Interactables.Interobjects.DoorUtils;
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
			if(!File.Exists(targetuseridpath)) return new PlayerData(DateTime.Now, userid, true, 0, 0, 0);
			else return ParsePlayerData(targetuseridpath);
		}

		public static void SavePlayerData(PlayerData data)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, $"{data.userid}.txt");

			if(!Directory.Exists(SanyaPlugin.Instance.Config.DataDirectory)) Directory.CreateDirectory(SanyaPlugin.Instance.Config.DataDirectory);

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

		public static void ReloadParams()
		{
			foreach(var file in Directory.GetFiles(SanyaPlugin.Instance.Config.DataDirectory))
			{
				if(!file.Contains("@")) continue;
				var data = LoadPlayerData(file.Replace(".txt", string.Empty));
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
						Log.Info($"[VPNChecker] VPN Detected:{address} UserId:{ev.UserId}");
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

		public static IEnumerator<float> CheckIsLimitedSteam(string userid)
		{
			PlayerData data = null;
			if(SanyaPlugin.Instance.Config.DataEnabled && PlayerDataManager.playersData.TryGetValue(userid, out data) && !data.limited)
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
								var player = Player.Get(userid);
								if(player != null)
								{
									ServerConsole.Disconnect(player.Connection, Subtitles.LimitedKickMessage);
								}

								if(!EventHandlers.kickedbyChecker.ContainsKey(userid))
									EventHandlers.kickedbyChecker.Add(userid, "steam");

								yield break;
							}
						}
						else
						{
							Log.Warn($"[SteamCheck] Falied(NoProfile):{userid}");
							var player = Player.Get(userid);
							if(player != null)
							{
								ServerConsole.Disconnect(player.Connection, Subtitles.NoProfileKickMessage);
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
		public static bool isAirBombGoing = false;

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

		public static IEnumerator<float> StartNightMode()
		{
			Log.Debug($"[StartNightMode] Started. Wait for {60}s...", SanyaPlugin.Instance.Config.IsDebugged);
			yield return Timing.WaitForSeconds(60f);
			if(SanyaPlugin.Instance.Config.CassieSubtitle)
				Methods.SendSubtitle(Subtitles.StartNightMode, 20);
			RespawnEffectsController.PlayCassieAnnouncement("warning . facility power system has been attacked . all most containment zones light does not available until generator activated .", false, true);
			SanyaPlugin.Instance.Handlers.IsEnableBlackout = true;
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
				isAirBombGoing = true;

			if(SanyaPlugin.Instance.Config.CassieSubtitle)
			{
				Methods.SendSubtitle(Subtitles.AirbombStarting, 10);
				RespawnEffectsController.PlayCassieAnnouncement("danger . outside zone emergency termination sequence activated .", false, true);
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

			if(SanyaPlugin.Instance.Config.CassieSubtitle)
			{
				Methods.SendSubtitle(Subtitles.AirbombEnded, 10);
				RespawnEffectsController.PlayCassieAnnouncement("outside zone termination completed .", false, true);
			}

			Log.Info($"[AirSupportBomb] Ended.");
			yield break;
		}

		public static IEnumerator<float> Scp049CureFromStack(Player player)
		{
			yield return Timing.WaitForOneFrame;

			if(player.Role != RoleType.Scp049 || SanyaPlugin.Instance.Handlers.scp049stackAmount <= 0 || player.ReferenceHub.fpc.forceStopInputs) yield break;

			player.ReferenceHub.fpc.NetworkforceStopInputs = true;
			player.EnableEffect<Amnesia>();
			yield return Timing.WaitForSeconds(5f);

			var comp = player.GameObject.GetComponent<SanyaPluginComponent>();
			var target = Player.List.Where(x => x.IsDead && x.MaxHealth != 0).Random();
			if(target != null)
			{
				target.SetRole(RoleType.Scp0492, true);
				target.Position = player.Position;
				SanyaPlugin.Instance.Handlers.scp049stackAmount--;
				comp?.AddHudCenterDownText("Success!", 3);
			}
			else
			{
				comp?.AddHudCenterDownText("Failed...", 3);
			}
			player.DisableEffect<Amnesia>();
			player.ReferenceHub.fpc.NetworkforceStopInputs = false;
		}

		public static IEnumerator<float> Scp106WalkingThrough(Player player)
		{
			yield return Timing.WaitForOneFrame;

			if(!Physics.Raycast(player.Position, -Vector3.up, 50f, player.ReferenceHub.scp106PlayerScript.teleportPlacementMask))
			{
				player.Position = RoleType.Scp106.GetRandomSpawnPointForConflict();
				yield break;
			}

			Vector3 forward = player.CameraTransform.forward;
			forward.Set(forward.x * 0.1f, 0f, forward.z * 0.1f);

			var hits = Physics.RaycastAll(player.Position, forward, 50f, 1);
			if(hits.Length < 2) yield break;
			if(hits[0].distance > 1f) yield break;

			if(!Physics.Raycast(hits.Last().point + forward, forward * -1f, out var BackHits, 50f, 1)) yield break;

			if(!PlayerMovementSync.FindSafePosition(BackHits.point, out var pos, true)) yield break;
			player.ReferenceHub.playerMovementSync.WhitelistPlayer = true;
			yield return Timing.WaitForOneFrame;
			player.ReferenceHub.fpc.NetworkforceStopInputs = true;
			player.AddItem(ItemType.SCP268);
			player.ReferenceHub.playerEffectsController.EnableEffect<Scp268>();
			player.ReferenceHub.playerEffectsController.EnableEffect<Deafened>();
			player.ReferenceHub.playerEffectsController.ChangeEffectIntensity<Visuals939>(1);
			//SanyaPlugin.Instance.Handlers.last106walkthrough.Restart();

			while(true)
			{
				if(player.Position == pos || player.Role != RoleType.Scp106)
				{
					player.ReferenceHub.fpc.NetworkforceStopInputs = false;
					player.ClearInventory();
					player.ReferenceHub.playerEffectsController.DisableEffect<Deafened>();
					player.ReferenceHub.playerEffectsController.DisableEffect<Visuals939>();
					yield return Timing.WaitForOneFrame;
					player.ReferenceHub.playerMovementSync.WhitelistPlayer = false;
					yield break;
				}
				player.Position = Vector3.MoveTowards(player.Position, pos, 0.25f);
				yield return Timing.WaitForOneFrame;
			}
		}

		public static IEnumerator<float> Scp106CustomTeleport(Scp106PlayerScript scp106PlayerScript, Vector3 position)
		{
			if(!scp106PlayerScript.goingViaThePortal)
			{
				scp106PlayerScript.RpcTeleportAnimation();
				scp106PlayerScript.goingViaThePortal = true;
				yield return Timing.WaitForSeconds(3.5f);
				scp106PlayerScript._hub.playerMovementSync.OverridePosition(position, 0f, false);
				yield return Timing.WaitForSeconds(7.5f);
				if(AlphaWarheadController.Host.detonated && scp106PlayerScript.transform.position.y < 800f)
					scp106PlayerScript._hub.playerStats.HurtPlayer(new PlayerStats.HitInfo(9000f, "WORLD", DamageTypes.Nuke, 0), scp106PlayerScript.gameObject, true);
				scp106PlayerScript.goingViaThePortal = false;
			}
		}

		public static IEnumerator<float> Scp939SetFake(ReferenceHub human, ReferenceHub scp939, RoleType targetRole, ItemType targetItem)
		{
			var Phuman = Player.Get(human);
			MirrorExtensions.SendFakeSyncVar(Phuman, scp939.networkIdentity, typeof(CharacterClassManager), nameof(CharacterClassManager.NetworkCurClass), (sbyte)targetRole);
			yield return Timing.WaitForSeconds(0.25f);
			MirrorExtensions.SendFakeSyncVar(Phuman, scp939.networkIdentity, typeof(Inventory), nameof(Inventory.Network_curItemSynced), (int)targetItem);
			yield break;
		}

		public static IEnumerator<float> ClassDInsurgencyInit()
		{
			var armorydoor = DoorNametagExtension.NamedDoors.First(x => x.Key == "LCZ_ARMORY").Value.TargetDoor;
			armorydoor.ServerChangeLock(DoorLockReason.AdminCommand, true);
			yield return Timing.WaitForSeconds(10f);
			Methods.SendSubtitle(Subtitles.ClassDInsurgencyFirst, 10);
			RespawnEffectsController.PlayCassieAnnouncement("danger . Detected security warning in light containment zones armory", false, true);
			armorydoor.ServerChangeLock(DoorLockReason.AdminCommand, false);
			armorydoor.NetworkTargetState = true;
			yield break;
		}

		public static IEnumerator<float> AlreadyBreakInit()
		{
			Lift.Instances.First(x => x.elevatorName == "GateB").UseLift();
			yield return Timing.WaitForSeconds(3f);
			Methods.SendSubtitle(Subtitles.AlreadyBreakFirst, 20);
			RespawnEffectsController.PlayCassieAnnouncement("attention all personnel . facility guards HasEntered . AllRemaining .", false, true);
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
			component.FullInitData(gm, position, Quaternion.Euler(component.throwStartAngle), Vector3.zero, component.throwAngularVelocity, player == null ? Team.TUT : player.characterClassManager.CurRole.team);
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

		public static void SendSubtitle(string text, ushort time, Player target = null)
		{
			if(target != null)
			{
				target.ClearBroadcasts();
				target.Broadcast(time, text);
			}
			else
			{
				Map.ClearBroadcasts();
				Map.Broadcast(time, text);
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

		public static void AddDeathTimeForScp049(this Player target)
		{
			PlayerManager.localPlayer.GetComponent<RagdollManager>().SpawnRagdoll(
				Vector3.zero,
				target.GameObject.transform.rotation,
				Vector3.zero,
				(int)RoleType.ClassD,
				new PlayerStats.HitInfo(-1, "Scp049Reviver", DamageTypes.Scp049, -1),
				true,
				target.GameObject.GetComponent<MirrorIgnorancePlayer>().PlayerId,
				target.Nickname,
				target.Id
			);
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

		public static void Blink()
		{
			foreach(var scp173 in UnityEngine.Object.FindObjectsOfType<Scp173PlayerScript>())
				scp173.RpcBlinkTime();
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
					Log.Warn($"Detect:{collider.name}");
					result = true;
				}
			}
			return result;
		}

		public static void Remove914Item(ItemType type)
		{
			foreach(var i in Scp914.Scp914Machine.singleton.recipesDict)
				foreach(var j in i.Value)
					for(int k = 0; k < j.Value.Length; k++)
						if(j.Value[k] == type)
							j.Value[k] = i.Key;
		}

		public static void Add914RecipeCoin()
		{
			Scp914.Scp914Machine.singleton.recipesDict.Add(ItemType.Coin, new System.Collections.Generic.Dictionary<Scp914.Scp914Knob, ItemType[]>()
						{
							{ Scp914.Scp914Knob.Rough, new[]{ItemType.None } },
							{ Scp914.Scp914Knob.Coarse, new[]{ItemType.None } },
							{ Scp914.Scp914Knob.OneToOne, new[]{ItemType.Coin } },
							{ Scp914.Scp914Knob.Fine, new[]{
								ItemType.KeycardJanitor,
								ItemType.KeycardScientist,
								ItemType.KeycardScientistMajor,
								ItemType.KeycardZoneManager,
								ItemType.KeycardGuard,
								ItemType.KeycardSeniorGuard,
								ItemType.KeycardContainmentEngineer,
								ItemType.KeycardNTFLieutenant,
								ItemType.KeycardNTFCommander,
								ItemType.KeycardFacilityManager,
								ItemType.KeycardChaosInsurgency,
								ItemType.KeycardO5,
								ItemType.GunCOM15,
								ItemType.MicroHID,
								ItemType.SCP207,
								ItemType.WeaponManagerTablet,
								ItemType.GunE11SR,
								ItemType.GunProject90,
								ItemType.GunMP7,
								ItemType.GunLogicer,
								ItemType.GrenadeFrag,
								ItemType.GrenadeFlash,
								ItemType.GunUSP,
								ItemType.SCP018,
								ItemType.SCP268,
								ItemType.Ammo556,
								ItemType.Ammo762,
								ItemType.Ammo9mm,
								ItemType.Medkit,
								ItemType.Adrenaline,
								ItemType.Painkillers,
								ItemType.SCP500,
								ItemType.Flashlight,
								ItemType.Radio,
								ItemType.Disarmer
							} },
							{ Scp914.Scp914Knob.VeryFine, new[]{
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.None,
								ItemType.KeycardFacilityManager,
								ItemType.KeycardContainmentEngineer,
								ItemType.KeycardO5,
								ItemType.MicroHID,
								ItemType.GunE11SR,
								ItemType.SCP500,
								ItemType.SCP018
							} }
						});
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

		public static void ShowHitmarker(this Player player)
		{
			player.ReferenceHub.GetComponent<Scp173PlayerScript>().TargetHitMarker(player.Connection);
		}

		public static void SendToTargetSound(this Player player)
		{
			NetworkServer.SendToClientOfPlayer(player.ReferenceHub.networkIdentity, new PlayableScps.Messages.Scp096ToTargetMessage(player.ReferenceHub));
		}

		public static void SendTextHint(this Player player, string text, float time)
		{
			player.ReferenceHub.hints.Show(new TextHint(text, new HintParameter[] { new StringHintParameter(string.Empty) }, new HintEffect[] { HintEffectPresets.TrailingPulseAlpha(0.5f, 1f, 0.5f, 2f, 0f, 2) }, time));
		}

		public static void SendTextHintNotEffect(this Player player, string text, float time)
		{
			player.ReferenceHub.hints.Show(new TextHint(text, new HintParameter[] { new StringHintParameter(string.Empty) }, null, time));
		}

		public static Vector3 GetRandomSpawnPointForConflict(this RoleType role) => Exiled.API.Extensions.Role.GetRandomSpawnPoint(role);

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

		public static bool IsExmode(this Player player) => player.ReferenceHub.animationController.curAnim == 1;

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
			return ie.ElementAt(SanyaPlugin.Instance.Random.Next(ie.Count()));
		}
	}
}