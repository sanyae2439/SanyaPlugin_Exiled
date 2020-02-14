using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using EXILED;
using UnityEngine;
using MEC;
using Utf8Json;
using Dissonance.Integrations.MirrorIgnorance;
using EXILED.Extensions;

namespace SanyaPlugin
{
    public class EventHandlers
    {
        internal readonly SanyaPlugin plugin;
        public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
        internal bool loaded = false;

        /** Infosender **/
        private readonly UdpClient udpClient = new UdpClient();
        internal Task sendertask;
        internal async Task _SenderAsync()
        {
            while(true)
            {
                try
                {
                    if(!this.loaded)
                    {
                        Log.Debug($"[Infosender_Task] Plugin not loaded. Skipped...");
                        await Task.Delay(TimeSpan.FromSeconds(15));
                    }

                    if(SanyaPluginConfig.infosender_ip == "none")
                    {
                        Log.Info($"[Infosender_Task] Disabled(config:({SanyaPluginConfig.infosender_ip}). breaked.");
                        break;
                    }

                    Serverinfo cinfo = new Serverinfo();

                    DateTime dt = DateTime.Now;
                    cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
                    cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
                    cinfo.modversion = $"{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}";
                    cinfo.sanyaversion = SanyaPlugin.Version;
                    cinfo.gamemode = "NONE";
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
                                ip = ReferenceHub.GetHub(player).characterClassManager.RequestIp,
                                role = ReferenceHub.GetHub(player).characterClassManager.CurClass.ToString(),
                                rank = ReferenceHub.GetHub(player).serverRoles.MyText
                            };

                            cinfo.players.Add(ply);
                        }
                    }

                    string json = JsonSerializer.ToJsonString(cinfo);

                    byte[] sendBytes = Encoding.UTF8.GetBytes(json);
                    udpClient.Send(sendBytes, sendBytes.Length, SanyaPluginConfig.infosender_ip, SanyaPluginConfig.infosender_port);
                    Log.Debug($"[Infosender_Task] {SanyaPluginConfig.infosender_ip}:{SanyaPluginConfig.infosender_port}");
                }
                catch(Exception e)
                {
                    throw e;
                }
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
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

                //寝返り
                if(SanyaPluginConfig.traitor_limitter > 0)
                {
                    foreach(var player in Player.GetHubs())
                    {
                        if((player.GetTeam() == Team.MTF || player.GetTeam() == Team.CHI)
                            && player.IsHandCuffed()
                            && Vector3.Distance(espaceArea, player.transform.position) <= Escape.radius
                            && RoundSummary.singleton.CountTeam(player.GetTeam()) <= SanyaPluginConfig.traitor_limitter)
                        {
                            switch(player.GetTeam())
                            {
                                case Team.MTF:
                                    if(UnityEngine.Random.Range(0, 100) <= SanyaPluginConfig.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : MTF->CHI");
                                        player.characterClassManager.SetPlayersClass(RoleType.ChaosInsurgency, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                                case Team.CHI:
                                    if(UnityEngine.Random.Range(0, 100) <= SanyaPluginConfig.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : CHI->MTF");
                                        player.characterClassManager.SetPlayersClass(RoleType.NtfCadet, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetName()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                            }
                        }
                    }
                }

                //毎秒
                yield return Timing.WaitForSeconds(1f);
            }
        }
        internal CoroutineHandle fixedUpdatehandle;
        internal IEnumerator<float> _FixedUpdate()
        {
            while(true)
            {
                yield return Timing.WaitForOneFrame;
            }
        }

        /** Flag Params **/
        internal static bool autowarheadstarted = false;
        private Vector3 espaceArea = new Vector3(177.5f, 985.0f, 29.0f);

        public void OnWaintingForPlayers()
        {
            loaded = true;

            if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation)
                sendertask = this._SenderAsync().StartSender();
            everySecondhandle = Timing.RunCoroutine(_EverySecond(), Segment.FixedUpdate);
            fixedUpdatehandle = Timing.RunCoroutine(_FixedUpdate(), Segment.FixedUpdate);

            PlayerDataManager.playersData.Clear();

            Log.Info($"[OnWaintingForPlayers] Waiting for Players...");
        }

        public void OnRoundStart()
        {
            Log.Info($"[OnRoundStart] Round Start!");
        }

        public void OnRoundEnd()
        {
            Log.Info($"[OnRoundEnd] Round Ended.");

            if(SanyaPluginConfig.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(string.IsNullOrEmpty(player.GetUserId())) continue;

                    if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                    {
                        if(player.GetRoleType() == RoleType.Spectator)
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(SanyaPluginConfig.level_exp_other);
                        }
                        else
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(SanyaPluginConfig.level_exp_win);
                        }
                    }
                }

                foreach(var data in PlayerDataManager.playersData.Values)
                {
                    data.lastUpdate = DateTime.Now;
                    data.playingcount++;
                    PlayerDataManager.SavePlayerData(data);
                }
            }
        }

        public void OnRoundRestart()
        {
            Log.Info($"[OnRoundRestart] Restarting...");

            Timing.KillCoroutines(everySecondhandle);
            Timing.KillCoroutines(fixedUpdatehandle);

            autowarheadstarted = false;
            CancelWarheadPatch.Locked = false;
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Info($"[OnPlayerJoin] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

            if(SanyaPluginConfig.data_enabled)
            {
                PlayerData data = PlayerDataManager.LoadPlayerData(ev.Player.GetUserId());

                if(!PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData.Add(ev.Player.GetUserId(), data);

                }

                if(SanyaPluginConfig.level_enabled)
                {
                    Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate);
                }
            }
        }

        public void OnPlayerLeave(PlayerLeaveEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Debug($"[OnPlayerLeave] {ev.Player.GetName()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");
        }

        public void OnStartItems(StartItemsEvent ev)
        {
            Log.Debug($"[OnStartItems] {ev.Role}");

            if(SanyaPluginConfig.defaultitems.TryGetValue(ev.Role, out List<ItemType> itemconfig) && itemconfig.Count > 0)
            {
                ev.StartItems = itemconfig;
            }
        }

        public void OnPlayerSetClass(SetClassEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress())) return;
            Log.Debug($"[OnPlayerSetClass] {ev.Player.GetName()} -> {ev.Role}");
        }

        public void OnPlayerHurt(ref PlayerHurtEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress()) || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerHurt:Before] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");

            DamageTypes.DamageType damageTypes = ev.Info.GetDamageType();
            if(damageTypes != DamageTypes.Nuke && damageTypes != DamageTypes.Decont && damageTypes != DamageTypes.Wall && damageTypes != DamageTypes.Tesla)
            {
                PlayerStats.HitInfo clinfo = ev.Info;

                //USPMultiplier
                if(damageTypes == DamageTypes.Usp)
                {
                    if(ev.Player.characterClassManager.IsAnyScp())
                    {
                        clinfo.Amount *= SanyaPluginConfig.damage_usp_multiplier_scp;
                    }
                    else
                    {
                        clinfo.Amount *= SanyaPluginConfig.damage_usp_multiplier_human;
                    }
                }

                //CuffedDivisor
                if(ev.Player.IsHandCuffed())
                {
                    clinfo.Amount /= SanyaPluginConfig.damage_divisor_cuffed;
                }

                //SCPsDivisor
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

                //*****Final*****
                ev.Info = clinfo;
            }

            Log.Debug($"[OnPlayerHurt:After] {ev.Attacker?.GetName()} -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetName()}");
        }

        public void OnPlayerDeath(ref PlayerDeathEvent ev)
        {
            if(string.IsNullOrEmpty(ev.Player.GetIpAddress()) || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerDeath] {ev.Killer?.GetName()} -{ev.Info.GetDamageName()}-> {ev.Player?.GetName()}");

            if(SanyaPluginConfig.data_enabled)
            {
                if(ev.Player.GetUserId() != ev.Killer.GetUserId()
                    && PlayerDataManager.playersData.ContainsKey(ev.Killer.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Killer.GetUserId()].AddExp(SanyaPluginConfig.level_exp_kill);
                }

                if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Player.GetUserId()].AddExp(SanyaPluginConfig.level_exp_death);
                }
            }

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

        public void OnPocketDimDeath(PocketDimDeathEvent ev)
        {
            Log.Debug($"[OnPocketDimDeath] {ev.Player.GetName()}");

            if(SanyaPluginConfig.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRoleType() == RoleType.Scp106)
                    {
                        if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(SanyaPluginConfig.level_exp_kill);
                        }
                    }
                }
            }

            if(SanyaPluginConfig.recovery_amount_scp106 > 0)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRoleType() == RoleType.Scp106)
                    {
                        player.playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp106);
                        player.GetComponent<Scp173PlayerScript>().TargetHitMarker(player.characterClassManager.connectionToClient);
                    }
                }
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

        public void OnPlayerDoorInteract(ref DoorInteractionEvent ev)
        {
            Log.Debug($"[OnPlayerDoorInteract] {ev.Player.GetName()}:{ev.Door.DoorName}:{ev.Door.permissionLevel}");

            if(SanyaPluginConfig.inventory_keycard_act && ev.Player.GetTeam() != Team.SCP && !ev.Player.serverRoles.BypassMode && !ev.Door.locked)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    Log.Debug($"[OnPlayerDoorInteract] inv:{item.id} parm:{string.Join(",", ev.Player.inventory.GetItemByID(item.id).permissions)}");

                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains(ev.Door.permissionLevel))
                    {
                        ev.Allow = true;
                    }
                }
            }
        }

        public void OnGeneratorUnlock(ref GeneratorUnlockEvent ev)
        {
            Log.Debug($"[OnGeneratorUnlock] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(SanyaPluginConfig.inventory_keycard_act && !ev.Player.serverRoles.BypassMode)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    Log.Debug($"[OnGeneratorUnlock] inv:{item.id} parm:{string.Join(",", ev.Player.inventory.GetItemByID(item.id).permissions)}");

                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("ARMORY_LVL_2"))
                    {
                        ev.Allow = true;
                    }
                }
            }

            if(ev.Allow && SanyaPluginConfig.generator_unlock_to_open)
            {
                ev.Generator.NetworkisDoorOpen = true;
                ev.Generator.RpcDoSound(true);
            }
        }

        public void OnGeneratorOpen(ref GeneratorOpenEvent ev)
        {
            Log.Debug($"[OnGeneratorOpen] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Generator.prevFinish && SanyaPluginConfig.generator_finish_to_lock) ev.Allow = false;
        }

        public void OnGeneratorClose(ref GeneratorCloseEvent ev)
        {
            Log.Debug($"[OnGeneratorClose] {ev.Player.GetName()} -> {ev.Generator.curRoom}");
            if(ev.Allow && ev.Generator.isTabletConnected && SanyaPluginConfig.generator_activating_opened) ev.Allow = false;
        }

        public void OnGeneratorFinish(ref GeneratorFinishEvent ev)
        {
            Log.Debug($"[OnGeneratorFinish] {ev.Generator.curRoom}");
            if(SanyaPluginConfig.generator_finish_to_lock) ev.Generator.NetworkisDoorOpen = false;
        }

        public void On914Upgrade(ref SCP914UpgradeEvent ev)
        {
            Log.Debug($"[On914Upgrade] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}");

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

        public void OnConsoleCommand(ConsoleCommandEvent ev)
        {
            Log.Debug($"[OnConsoleCommand] [Before] Called:{ev.Player.GetName()} Command:{ev.Command} Return:{ev.ReturnMessage}");

            //if(ev.ReturnMessage == "Command not found.")
            //{
            //    //switch(ev.Command)
            //    {
            //        case "attack":
            //            var scp049 = ev.Player.GetComponent<Scp049PlayerScript>();
            //            var wm = ev.Player.weaponManager;
            //            Vector3 forward = scp049.plyCam.transform.forward;
            //            Vector3 position = scp049.plyCam.transform.position;
            //            RaycastHit raycastHit;
            //            if(Physics.Raycast(new Ray(position, forward), out raycastHit, scp049.attackDistance, wm.raycastMask))
            //            {
            //                Log.Debug($"{Plugin.GetPlayer(raycastHit.transform.gameObject).GetName()}");
            //            }
            //            ev.ReturnMessage = "attacked.";
            //            break;
            //        default:
            //            break;
            //    }
            //}

            Log.Debug($"[OnConsoleCommand] [After] Called:{ev.Player.GetName()} Command:{ev.Command} Return:{ev.ReturnMessage}");
        }

        public void OnCommand(ref RACommandEvent ev)
        {
            string[] args = ev.Command.Split(' ');
            if(args[0].ToLower() == "sanya")
            {
                if(args.Length > 1)
                {
                    string ReturnStr;
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
                        case "spawn":
                            var mtfrespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                            if(mtfrespawn.nextWaveIsCI)
                            {
                                mtfrespawn.timeToNextRespawn = 14f;
                            }
                            else
                            {
                                mtfrespawn.timeToNextRespawn = 18.5f;
                            }
                            ReturnStr = $"spawn soon.";
                            break;
                        default:
                            ReturnStr = "Wrong Parameters.";
                            break;
                    }
                    ev.Allow = false;
                    ev.Sender.RAMessage(ReturnStr);
                }
                else
                {
                    ev.Allow = false;
                    ev.Sender.RAMessage("Usage : SANYA < CONFIG / RELOAD / NUKELOCK / SPAWN / TEST >");
                }
            }
        }
    }
}
