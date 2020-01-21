using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using GameCore;
using EXILED;
using UnityEngine;
using MEC;
using Utf8Json;
using Harmony;

namespace SanyaPlugin
{
    public class EventHandlers
    {
        internal readonly SanyaPlugin plugin;
        public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;

        /** Infosender **/
        private UdpClient udpClient = new UdpClient();

        private IEnumerator<float> Sender()
        {
            while(true)
            {
                try
                {
                    if(SanyaPluginConfig.infosender_ip == "none")
                    {
                        Plugin.Info($"[Infosender] Disabled(config:({SanyaPluginConfig.infosender_ip}). breaked coroutine.");
                        break;
                    }

                    Serverinfo cinfo = new Serverinfo();

                    DateTime dt = DateTime.Now;
                    cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
                    cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
                    cinfo.modversion = Regex.Replace(Regex.Match(ServerConsole.singleton.RefreshServerName(), @"SM\d\d\d.\d.\d.\d").Value, "SM", "");
                    cinfo.sanyaversion = SanyaPlugin.Version;
                    cinfo.gamemode = "NONE";
                    cinfo.name = ConfigFile.ServerConfig.GetString("server_name", "My Server Name");
                    cinfo.ip = ServerConsole.Ip;
                    cinfo.port = ServerConsole.Port;
                    cinfo.playing = PlayerManager.players.Count;
                    cinfo.maxplayer = CustomNetworkManager.slots;
                    cinfo.duration = RoundSummary.roundTime;

                    if(cinfo.playing > 0)
                    {
                        foreach(GameObject player in PlayerManager.players)
                        {
                            Playerinfo ply = new Playerinfo();

                            ply.name = ReferenceHub.GetHub(player).nicknameSync.MyNick;
                            ply.userid = ReferenceHub.GetHub(player).characterClassManager.SyncedUserId;
                            ply.ip = ReferenceHub.GetHub(player).characterClassManager.RequestIp;
                            ply.role = ReferenceHub.GetHub(player).characterClassManager.CurClass.ToString();
                            ply.rank = ReferenceHub.GetHub(player).serverRoles.MyText;

                            cinfo.players.Add(ply);
                        }
                    }

                    string json = JsonSerializer.ToJsonString(cinfo);

                    byte[] sendBytes = Encoding.UTF8.GetBytes(json);
                    udpClient.Send(sendBytes, sendBytes.Length, SanyaPluginConfig.infosender_ip, SanyaPluginConfig.infosender_port);
                    Plugin.Debug($"[Infosender] {SanyaPluginConfig.infosender_ip}:{SanyaPluginConfig.infosender_port}");
                }
                catch(Exception e)
                {
                    Plugin.Error($"[Infosender] {e.ToString()}");
                    yield break;
                }
                yield return Timing.WaitForSeconds(15f);
            }
            yield break;
        }

        public void OnWaintingForPlayers()
        {
            Timing.KillCoroutines("SanyaPlugin_Sender");
            Timing.RunCoroutine(Sender(), Segment.FixedUpdate,"SanyaPlugin_Sender");

            Plugin.Info($"[OnWaintingForPlayers] Waiting for Players...");
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.characterClassManager.RequestIp)) return;
            Plugin.Info($"[OnPlayerJoin] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");
        }

        public void OnPlayerLeave(PlayerLeaveEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Debug($"[OnPlayerLeave] {ev.Player.GetName()}");
        }

        public void OnPlayerSetClass(SetClassEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Debug($"[OnPlayerSetClass] {ev.Player.nicknameSync.MyNick} -> {ev.Role}");

            List<ItemType> itemconfig;
            if(SanyaPluginConfig.defaultitems.TryGetValue(ev.Role, out itemconfig) && itemconfig.Count > 0)
            {
                ev.Player.characterClassManager.Classes.SafeGet(ev.Role).startItems = itemconfig.ToArray();
            }

        }

        public void OnPlayerHurt(ref PlayerHurtEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Debug($"[OnPlayerHurt] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");

        }

        public void OnPlayerTriggerTesla(ref TriggerTeslaEvent ev)
        {
            if(SanyaPluginConfig.tesla_triggerable_teams.Count == 0 
                || SanyaPluginConfig.tesla_triggerable_teams.Contains((int)Plugin.GetTeam(ev.Player.GetRoleType())))
            {
                if(SanyaPluginConfig.tesla_triggerable_disarmed || ev.Player.handcuffs.CufferId == -1)
                {
                    ev.Triggerable = true;
                }
                else
                {
                    ev.Triggerable = false;
                }
            }
            else
            {
                ev.Triggerable = false;
            }
        }
    }
}
