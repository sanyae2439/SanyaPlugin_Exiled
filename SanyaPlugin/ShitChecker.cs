using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using Exiled.API.Features;
using Exiled.Events.EventArgs;
using MEC;
using UnityEngine.Networking;
using Utf8Json;

namespace SanyaPlugin
{
	public class ShitChecker
	{
		private HashSet<IPAddress> IPWhiteList { get; set; } = new HashSet<IPAddress>();
		private HashSet<IPAddress> IPBlackList { get; set; } = new HashSet<IPAddress>();
		public string PathWhiteList { get; private set; }
		public string PathBlackList { get; private set; }

		public void LoadLists()
		{
			PathWhiteList = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, "VPN-Whitelist.txt");
			PathBlackList = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, "VPN-Blacklist.txt");
			IPWhiteList.Clear();
			IPBlackList.Clear();

			if(!File.Exists(PathWhiteList))
				File.WriteAllText(PathWhiteList, null);
			if(!File.Exists(PathBlackList))
				File.WriteAllText(PathBlackList, null);

			foreach(var line in File.ReadAllLines(PathWhiteList))
				if(IPAddress.TryParse(line, out var address))
					IPWhiteList.Add(address);

			foreach(var line2 in File.ReadAllLines(PathBlackList))
				if(IPAddress.TryParse(line2, out var address2))
					IPBlackList.Add(address2);
		}

		public IEnumerator<float> CheckVPN(PreAuthenticatingEventArgs ev)
		{
			IPAddress address = ev.Request.RemoteEndPoint.Address;

			if(IsWhiteListed(address) || IsBlackListed(address))
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
						AddWhiteList(address);
						yield break;
					}
					else if(data.block == 1)
					{
						Log.Warn($"[VPNChecker] VPN Detected:{address} UserId:{ev.UserId}");
						AddBlackList(address);

						var player = Player.Get(ev.UserId);
						if(player != null)
						{
							ServerConsole.Disconnect(player.Connection, SanyaPlugin.Instance.Translation.VpnKickMessage);
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

		public IEnumerator<float> CheckSteam(string userid)
		{
			PlayerData data = null;
			if(SanyaPlugin.Instance.Config.DataEnabled && SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.TryGetValue(userid, out data)
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
									SanyaPlugin.Instance.PlayerDataManager.SavePlayerData(data);
								}
							}
							else
							{
								Log.Warn($"[SteamCheck:VacBanned] NG:{userid}");
								var player = Player.Get(userid);
								if(player != null)
									ServerConsole.Disconnect(player.Connection, SanyaPlugin.Instance.Translation.VacBannedKickMessage);

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
									SanyaPlugin.Instance.PlayerDataManager.SavePlayerData(data);
								}
							}
							else
							{
								Log.Warn($"[SteamCheck:Limited] NG:{userid}");
								var player = Player.Get(userid);
								if(player != null)
								{
									ServerConsole.Disconnect(player.Connection, SanyaPlugin.Instance.Translation.LimitedKickMessage);
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
								ServerConsole.Disconnect(player.Connection, SanyaPlugin.Instance.Translation.NoProfileKickMessage);
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

		public void AddWhiteList(IPAddress address)
		{
			IPWhiteList.Add(address);
			using(StreamWriter writer = File.AppendText(PathWhiteList))
			{
				writer.WriteLine(address);
			}
		}

		public void AddBlackList(IPAddress address)
		{
			IPBlackList.Add(address);
			using(StreamWriter writer = File.AppendText(PathBlackList))
			{
				writer.WriteLine(address);
			}
		}

		public bool IsWhiteListed(IPAddress address) => IPWhiteList.Contains(address);

		public bool IsBlackListed(IPAddress address) => IPBlackList.Contains(address);
	}
}
