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
using Dissonance.Integrations.MirrorIgnorance;

namespace SanyaPlugin
{
    public class EventHandlers
    {
        internal readonly SanyaPlugin plugin;
        public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;

        /** Infosender **/
        private UdpClient udpClient = new UdpClient();
        internal CoroutineHandle infosenderhandle;
        internal IEnumerator<float> _Sender()
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
                            ply.userid = ReferenceHub.GetHub(player).characterClassManager.UserId;
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

        /** Update **/
        internal CoroutineHandle everySecondhandle;
        internal IEnumerator<float> _EverySecond()
        {
            while(true)
            {
                //自動核 & 自動核ロック -> CancelWarheadPatch
                if(SanyaPluginConfig.auto_warhead_start > 0 && !autowarheadstarted)
                {
                    if(RoundSummary.roundTime >= SanyaPluginConfig.auto_warhead_start)
                    {
                        autowarheadstarted = true;
                        if(SanyaPluginConfig.auto_warhead_start_lock) CancelWarheadPatch.Locked = true;
                        AlphaWarheadOutsitePanel.nukeside.Networkenabled = true;
                        AlphaWarheadController.Host.NetworkinProgress = true;
                    }
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }
        internal CoroutineHandle fixedUpdatehandle;
        internal IEnumerator<float> _FixedUpdate()
        {
            while(true)
            {
                //foreach(var player in Plugin.GetHubs())
                //{
                //    Scp106PlayerScript p106 = player.GetComponent<Scp106PlayerScript>();

                //    if(p106.portalPrefab != null && Plugin.GetTeam(player.GetRoleType()) != Team.SCP)
                //    {
                //        bool isrange = Vector3.Distance(player.transform.position, p106.portalPrefab.transform.position) < 2.5f;
                //        bool isenable = player.effectsController.GetEffect<SinkHole>("SinkHole").Enabled;

                //        if(isrange && !isenable)
                //        {
                //            player.effectsController.EnableEffect("SinkHole");
                //        }
                //        else if(!isrange && isenable)
                //        {
                //            player.effectsController.DisableEffect("SinkHole");
                //        }
                //    }
                //}
                yield return Timing.WaitForOneFrame;
            }
        }

        /** Flag Params **/
        internal static bool autowarheadstarted = false;

        public void OnWaintingForPlayers()
        {
            infosenderhandle = Timing.RunCoroutine(_Sender(), Segment.FixedUpdate);
            everySecondhandle = Timing.RunCoroutine(_EverySecond(), Segment.FixedUpdate);
            fixedUpdatehandle = Timing.RunCoroutine(_FixedUpdate(), Segment.FixedUpdate);

            Plugin.Info($"[OnWaintingForPlayers] Waiting for Players...");
        }

        public void OnRoundStart()
        {
            Plugin.Info($"[OnRoundStart] Round Start!");
        }

        public void OnRoundEnd()
        {
            Plugin.Info($"[OnRoundEnd] Round Ended.");
        }

        public void OnRoundRestart()
        {
            Plugin.Info($"[OnRoundRestart] Restarting...");

            Timing.KillCoroutines(infosenderhandle);
            Timing.KillCoroutines(everySecondhandle);
            Timing.KillCoroutines(fixedUpdatehandle);

            autowarheadstarted = false;
            CancelWarheadPatch.Locked = false;
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Info($"[OnPlayerJoin] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");
        }

        public void OnPlayerLeave(PlayerLeaveEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Info($"[OnPlayerLeave] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");
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
            Plugin.Debug($"[OnPlayerHurt:Before] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");

            DamageTypes.DamageType damageTypes = ev.Info.GetDamageType();
            if(damageTypes != DamageTypes.Nuke && damageTypes != DamageTypes.Decont && damageTypes != DamageTypes.Wall && damageTypes != DamageTypes.Tesla)
            {
                PlayerStats.HitInfo clinfo = ev.Info;
                switch(ev.Player.GetRoleType())
                {
                    case RoleType.Scp173:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp173;
                        break;
                    case RoleType.Scp106:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp106;
                        break;
                    case RoleType.Scp049:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp049;
                        break;
                    case RoleType.Scp096:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp096;
                        break;
                    case RoleType.Scp0492:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp0492;
                        break;
                    case RoleType.Scp93953:
                    case RoleType.Scp93989:
                        clinfo.Amount /= SanyaPluginConfig.damage_divisor_scp939;
                        break;
                }
                ev.Info = clinfo;
            }

            Plugin.Debug($"[OnPlayerHurt:After] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");
        }

        public void OnPlayerDeath(ref PlayerDeathEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Plugin.Debug($"[OnPlayerDeath] {ev.Killer?.GetName()} -{ev.Info.GetDamageName()}-> {ev.Player?.GetName()}");

            if(ev.Info.GetDamageType() == DamageTypes.Scp173 && ev.Killer.GetRoleType() == RoleType.Scp173 && SanyaPluginConfig.recovery_amount_scp173 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp173);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp096 && ev.Killer.GetRoleType() == RoleType.Scp096 && SanyaPluginConfig.recovery_amount_scp096 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp096);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp939 && (ev.Killer.GetRoleType() == RoleType.Scp93953 || ev.Killer.GetRoleType() == RoleType.Scp93989) && SanyaPluginConfig.recovery_amount_scp939 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp939);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp0492 && ev.Killer.GetRoleType() == RoleType.Scp0492 && SanyaPluginConfig.recovery_amount_scp0492 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp0492);
            }
        }

        public void OnPlayerTriggerTesla(ref TriggerTeslaEvent ev)
        {
            if(SanyaPluginConfig.tesla_triggerable_teams.Count == 0
                || SanyaPluginConfig.tesla_triggerable_teams.Contains((int)ev.Player.GetTeam()))
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

        public void OnGeneratorUnlock(ref GeneratorUnlockEvent ev)
        {
            Plugin.Debug($"[OnGeneratorUnlock] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Allow && SanyaPluginConfig.generator_unlock_to_open) ev.Generator.NetworkisDoorOpen = true;
        }

        public void OnGeneratorOpen(ref GeneratorOpenEvent ev)
        {
            Plugin.Debug($"[OnGeneratorOpen] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Generator.prevFinish && SanyaPluginConfig.generator_finish_to_lock) ev.Allow = false;
        }

        public void OnGeneratorClose(ref GeneratorCloseEvent ev)
        {
            Plugin.Debug($"[OnGeneratorClose] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Allow && ev.Generator.isTabletConnected && SanyaPluginConfig.generator_activating_opened) ev.Allow = false;
        }

        public void OnGeneratorFinish(ref GeneratorFinishEvent ev)
        {
            Plugin.Debug($"[OnGeneratorFinish] {ev.Generator.curRoom}");
            if(SanyaPluginConfig.generator_finish_to_lock) ev.Generator.NetworkisDoorOpen = false;
        }

        public void On914Upgrade(ref SCP914UpgradeEvent ev)
        {
            Plugin.Debug($"[On914Upgrade] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}");

            if(SanyaPluginConfig.scp914_intake_death)
            {
                foreach(var player in ev.Players)
                {
                    var info = new PlayerStats.HitInfo(999999, "WORLD", DamageTypes.RagdollLess, 0);
                    UnityEngine.Object.FindObjectOfType<RagdollManager>().SpawnRagdoll(ev.Machine.output.position,
                                                                       player.transform.rotation,
                                                                       (int)player.GetRoleType(),
                                                                       info,
                                                                       false,
                                                                       player.GetComponent<MirrorIgnorancePlayer>().PlayerId,
                                                                       player.GetName(),
                                                                       player.queryProcessor.PlayerId
                                                                       );
                    player.playerStats.HurtPlayer(info, player.gameObject);
                }
            }
        }

        public void OnCommand(ref RACommandEvent ev)
        {
            if(ev.Command.Contains("REQUEST_DATA PLAYER_LIST SILENT")) return;

            string[] args = ev.Command.Split(' ');
            ReferenceHub sender = Plugin.GetPlayer(ev.Sender.SenderId);
            string ReturnStr = "";

            if(args[0].ToLower() == "sanya")
            {
                if(args.Length > 1)
                {
                    switch(args[1].ToLower())
                    {
                        case "test":
                            ReturnStr = "test ok.";
                            break;
                        case "config":
                            ReturnStr = SanyaPluginConfig.GetConfigs();
                            break;
                        case "reload":
                            SanyaPluginConfig.Reload();
                            ReturnStr = "reload ok";
                            break;
                        case "nukelock":
                            CancelWarheadPatch.Locked = !CancelWarheadPatch.Locked;
                            ReturnStr = $"nukelock:{CancelWarheadPatch.Locked}";
                            break;
                        default:
                            ReturnStr = "Wrong Parameters.";
                            break;
                    }
                    ev.Allow = false;
                    ev.Sender.RaReply("SanyaPlugin#" + ReturnStr, true, true, string.Empty);
                }
                else
                {
                    ev.Allow = false;
                    ev.Sender.RaReply("SanyaPlugin#Usage : SANYA < TEST >)", true, true, string.Empty);
                }
            }
        }
    }
}
