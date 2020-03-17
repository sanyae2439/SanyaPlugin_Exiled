using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using EXILED;
using UnityEngine;
using Mirror;
using MEC;
using Utf8Json;
using EXILED.Extensions;
using CustomPlayerEffects;

namespace SanyaPlugin
{
    public class EventHandlers
    {
        public EventHandlers(SanyaPlugin plugin) => this.plugin = plugin;
        internal readonly SanyaPlugin plugin;
        internal List<CoroutineHandle> roundCoroutines = new List<CoroutineHandle>();
        internal bool loaded = false;

        /** Infosender **/
        private readonly UdpClient udpClient = new UdpClient();
        internal Task sendertask;
        private bool senderdisabled = false;
        internal async Task _SenderAsync()
        {
            Log.Debug($"[Infosender_Task] Started.");

            while(true)
            {
                try
                {
                    if(Configs.infosender_ip == "none")
                    {
                        Log.Info($"[Infosender_Task] Disabled(config:({Configs.infosender_ip}). breaked.");
                        senderdisabled = true;
                        break;
                    }

                    if(!this.loaded)
                    {
                        Log.Debug($"[Infosender_Task] Plugin not loaded. Skipped...");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }

                    Serverinfo cinfo = new Serverinfo();

                    DateTime dt = DateTime.Now;
                    cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
                    cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
                    cinfo.modversion = $"{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}";
                    cinfo.sanyaversion = SanyaPlugin.Version;
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
                    udpClient.Send(sendBytes, sendBytes.Length, Configs.infosender_ip, Configs.infosender_port);
                    Log.Debug($"[Infosender_Task] {Configs.infosender_ip}:{Configs.infosender_port}");
                }
                catch(Exception e)
                {
                    throw e;
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }

        /** Update **/
        internal IEnumerator<float> _EverySecond()
        {
            while(true)
            {
                //自動核 & 自動核ロック -> CancelWarheadPatch
                if(Configs.auto_warhead_start > 0 && !autowarheadstarted)
                {
                    if(RoundSummary.roundTime >= Configs.auto_warhead_start)
                    {
                        Log.Debug("[AutoWarhead] Fired!");
                        autowarheadstarted = true;
                        if(Configs.auto_warhead_start_lock) IsNukeLocked = true;
                        AlphaWarheadOutsitePanel.nukeside.Networkenabled = true;
                        if(Configs.cassie_subtitle && !AlphaWarheadController.Host.NetworkinProgress)
                        {
                            bool isresumed = AlphaWarheadController._resumeScenario != -1;
                            double left = isresumed ? AlphaWarheadController.Host.timeToDetonation : AlphaWarheadController.Host.timeToDetonation - 4;
                            double count = Math.Truncate(left / 10.0) * 10.0;

                            if(!isresumed)
                            {
                                Methods.SendSubtitle(Subtitles.AlphaWarheadStart.Replace("{0}", count.ToString()), 15);
                            }
                            else
                            {
                                Methods.SendSubtitle(Subtitles.AlphaWarheadResume.Replace("{0}", count.ToString()), 10);
                            }


                        }

                        if(Configs.fix_doors_on_countdown && AlphaWarheadController.Host.NetworkinProgress)
                        {
                            foreach(var door in UnityEngine.Object.FindObjectsOfType<Door>())
                            {
                                if(!door.warheadlock)
                                {
                                    if(!door.isOpen)
                                    {
                                        door.RpcDoSound();
                                    }
                                    door.warheadlock = true;
                                    door.moving.moving = true;
                                    door.SetState(open: true);
                                    door.UpdateLock();
                                }
                            }
                        }

                        AlphaWarheadController.Host.NetworkinProgress = true;
                    }
                }

                //自動空爆
                if(Configs.outsidezone_termination_time_after_nuke > 0
                    && detonatedDuration != -1
                    && RoundSummary.roundTime > (Configs.outsidezone_termination_time_after_nuke + detonatedDuration))
                {
                    roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb()));
                    detonatedDuration = -1;
                }

                //寝返り
                if(Configs.traitor_limitter > 0)
                {
                    foreach(var player in Player.GetHubs())
                    {
                        if((player.GetTeam() == Team.MTF || player.GetTeam() == Team.CHI)
                            && player.IsHandCuffed()
                            && Vector3.Distance(espaceArea, player.transform.position) <= Escape.radius
                            && RoundSummary.singleton.CountTeam(player.GetTeam()) <= Configs.traitor_limitter)
                        {
                            switch(player.GetTeam())
                            {
                                case Team.MTF:
                                    if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : MTF->CHI");
                                        player.characterClassManager.SetPlayersClass(RoleType.ChaosInsurgency, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                                case Team.CHI:
                                    if(UnityEngine.Random.Range(0, 100) <= Configs.traitor_chance_percent)
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : CHI->MTF");
                                        player.characterClassManager.SetPlayersClass(RoleType.NtfCadet, player.gameObject);
                                    }
                                    else
                                    {
                                        Log.Info($"[_EverySecond:Traitor] {player.GetNickname()} : Traitor Failed(by percent)");
                                        player.characterClassManager.SetPlayersClass(RoleType.Spectator, player.gameObject);
                                    }
                                    break;
                            }
                        }
                    }
                }

                //RagdollCleanup
                if(Configs.ragdoll_cleanup > 0)
                {
                    List<GameObject> nowragdolls = null;

                    foreach(var i in RagdollCleanupPatch.ragdolls)
                    {
                        if(Time.time - i.Value > Configs.ragdoll_cleanup && i.Key != null)
                        {
                            if(nowragdolls == null) nowragdolls = new List<GameObject>();
                            Log.Debug($"[RagdollCleanupPatch] Cleanup:{i.Key.transform.position} {Time.time - i.Value} > {Configs.ragdoll_cleanup}");
                            nowragdolls.Add(i.Key);
                        }
                    }

                    if(nowragdolls != null)
                    {
                        foreach(var x in nowragdolls)
                        {
                            RagdollCleanupPatch.ragdolls.Remove(x);
                            NetworkServer.Destroy(x);
                        }
                    }
                }

                //ItemCleanup
                if(Configs.item_cleanup > 0)
                {
                    List<GameObject> nowitems = null;

                    foreach(var i in ItemCleanupPatch.items)
                    {
                        if(Time.time - i.Value > Configs.item_cleanup && i.Key != null)
                        {
                            if(nowitems == null) nowitems = new List<GameObject>();
                            Log.Debug($"[ItemCleanupPatch] Cleanup:{i.Key.transform.position} {Time.time - i.Value} > {Configs.item_cleanup}");
                            nowitems.Add(i.Key);
                        }
                    }

                    if(nowitems != null)
                    {
                        foreach(var x in nowitems)
                        {
                            ItemCleanupPatch.items.Remove(x);
                            NetworkServer.Destroy(x);
                        }
                    }
                }

                //停電時強制再収用の際復電
                if(eventmode == SANYA_GAME_MODE.NIGHT && IsEnableBlackout && Generator079.mainGenerator.forcedOvercharge)
                {
                    IsEnableBlackout = false;
                }

                //毎秒
                yield return Timing.WaitForSeconds(1f);
            }
        }
        internal IEnumerator<float> _FixedUpdate()
        {
            while(true)
            {
                //Blackouter
                if(flickerableLight != null && IsEnableBlackout && flickerableLight.remainingFlicker < 0f && !flickerableLight.IsDisabled())
                {
                    //Log.Debug($"{UnityEngine.Object.FindObjectOfType<FlickerableLight>().remainingFlicker}");
                    Log.Debug($"[Blackouter] Fired.");
                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
                }

                yield return Timing.WaitForOneFrame;
            }
        }

        /** Flag Params **/
        internal static bool autowarheadstarted = false;
        internal static bool IsNukeLocked = false;
        private int detonatedDuration = -1;
        private Vector3 espaceArea = new Vector3(177.5f, 985.0f, 29.0f);

        /** RoundVar **/
        private FlickerableLight flickerableLight = null;
        private bool IsEnableBlackout = false;
        private bool IsPrevSpawnChaos = false;

        /** EventModeVar **/
        internal static SANYA_GAME_MODE eventmode = SANYA_GAME_MODE.NULL;
        private Vector3 LCZArmoryPos;

        public void OnWaintingForPlayers()
        {
            loaded = true;

            if(sendertask?.Status != TaskStatus.Running && sendertask?.Status != TaskStatus.WaitingForActivation && !senderdisabled)
                sendertask = _SenderAsync().StartSender();

            roundCoroutines.Add(Timing.RunCoroutine(_EverySecond(), Segment.FixedUpdate));
            roundCoroutines.Add(Timing.RunCoroutine(_FixedUpdate(), Segment.FixedUpdate));

            PlayerDataManager.playersData.Clear();
            RagdollCleanupPatch.ragdolls.Clear();
            ItemCleanupPatch.items.Clear();
            Coroutines.isAirBombGoing = false;

            autowarheadstarted = false;
            detonatedDuration = -1;
            IsNukeLocked = false;
            IsPrevSpawnChaos = false;
            IsEnableBlackout = false;

            flickerableLight = UnityEngine.Object.FindObjectOfType<FlickerableLight>();

            if(Configs.fix_doors_on_countdown)
            {
                foreach(var door in UnityEngine.Object.FindObjectsOfType<Door>())
                {
                    if(door.DoorName == "NUKE_SURFACE")
                    {
                        door.dontOpenOnWarhead = false;
                    }
                    else if(door.DoorName.Contains("GATE"))
                    {
                        door.dontOpenOnWarhead = true;
                    }
                }
            }

            eventmode = (SANYA_GAME_MODE)Methods.GetRandomIndexFromWeight(Configs.event_mode_weight.ToArray());
            switch(eventmode)
            {
                case SANYA_GAME_MODE.NIGHT:
                    {
                        break;
                    }
                case SANYA_GAME_MODE.CLASSD_INSURGENCY:
                    {
                        foreach(var room in Map.GetRooms())
                        {
                            if(room.Name == "LCZ_Armory")
                            {
                                LCZArmoryPos = room.Position + new Vector3(0, 2, 0);
                            }
                        }
                        break;
                    }
                default:
                    {
                        eventmode = SANYA_GAME_MODE.NORMAL;
                        break;
                    }
            }

            Log.Info($"[OnWaintingForPlayers] Waiting for Players... EventMode:{eventmode}");
        }

        public void OnRoundStart()
        {
            Log.Info($"[OnRoundStart] Round Start!");

            switch(eventmode)
            {
                case SANYA_GAME_MODE.NIGHT:
                    {
                        IsEnableBlackout = true;
                        roundCoroutines.Add(Timing.RunCoroutine(Coroutines.StartNightMode()));
                        break;
                    }
            }
        }

        public void OnRoundEnd()
        {
            Log.Info($"[OnRoundEnd] Round Ended.");

            if(Configs.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(string.IsNullOrEmpty(player.GetUserId())) continue;

                    if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                    {
                        if(player.GetRole() == RoleType.Spectator)
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_other);
                        }
                        else
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_win);
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

            if(Configs.godmode_after_endround)
            {
                foreach(var player in Player.GetHubs())
                {
                    player.characterClassManager.GodMode = true;
                }
            }

            Coroutines.isAirBombGoing = false;
        }

        public void OnRoundRestart()
        {
            Log.Info($"[OnRoundRestart] Restarting...");

            foreach(var cor in roundCoroutines)
                Timing.KillCoroutines(cor);
            roundCoroutines.Clear();
        }

        public void OnWarheadCancel(WarheadCancelEvent ev)
        {
            Log.Debug($"[OnWarheadCancel] {ev.Player?.GetNickname()} Locked:{IsNukeLocked}");

            if(IsNukeLocked)
            {
                ev.Allow = false;
                return;
            }

            if(Configs.cassie_subtitle && AlphaWarheadController.Host.NetworkinProgress && AlphaWarheadController.Host.timeToDetonation > 10f)
            {
                Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
            }

            if(Configs.fix_doors_on_countdown)
            {
                foreach(var door in UnityEngine.Object.FindObjectsOfType<Door>())
                {
                    if(door.warheadlock)
                    {
                        if(door.isOpen)
                        {
                            door.RpcDoSound();
                        }
                        door.moving.moving = true;
                        door.SetState(false);
                    }
                }
            }
        }

        public void OnDetonated()
        {
            Log.Debug($"[OnDetonated] Detonated:{(RoundSummary.roundTime / 60).ToString("00")}:{(RoundSummary.roundTime % 60).ToString("00")}");

            detonatedDuration = RoundSummary.roundTime;

            if(Configs.stop_respawn_after_detonated)
            {
                PlayerManager.localPlayer.GetComponent<MTFRespawn>().SummonChopper(false);
            }
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            if(ev.Player.IsHost()) return;
            Log.Info($"[OnPlayerJoin] {ev.Player.GetNickname()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

            if(!string.IsNullOrEmpty(Configs.motd_message))
            {
                Methods.TargetSendSubtitle(ev.Player, Configs.motd_message.Replace("[name]", ev.Player.GetNickname()), 10, false);
            }

            if(Configs.data_enabled)
            {
                PlayerData data = PlayerDataManager.LoadPlayerData(ev.Player.GetUserId());

                if(!PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData.Add(ev.Player.GetUserId(), data);

                }

                if(Configs.level_enabled)
                {
                    Timing.RunCoroutine(Coroutines.GrantedLevel(ev.Player, data), Segment.FixedUpdate);
                }
            }
        }

        public void OnPlayerLeave(PlayerLeaveEvent ev)
        {
            if(ev.Player.IsHost()) return;
            Log.Debug($"[OnPlayerLeave] {ev.Player.GetNickname()} ({ev.Player.GetIpAddress()}:{ev.Player.GetUserId()})");

            if(Configs.data_enabled && !string.IsNullOrEmpty(ev.Player.GetUserId()))
            {
                if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData.Remove(ev.Player.GetUserId());
                }
            }
        }

        public void OnStartItems(StartItemsEvent ev)
        {
            if(ev.Player.IsHost()) return;
            Log.Debug($"[OnStartItems] {ev.Player.GetNickname()} -> {ev.Role}");

            if(Configs.defaultitems.TryGetValue(ev.Role, out List<ItemType> itemconfig) && itemconfig.Count > 0)
            {
                ev.StartItems = itemconfig;
            }

            switch(eventmode)
            {
                case SANYA_GAME_MODE.CLASSD_INSURGENCY:
                    {
                        if(ev.Role == RoleType.ClassD && Configs.classd_insurgency_classd_inventory.Count > 0)
                        {
                            ev.StartItems = Configs.classd_insurgency_classd_inventory;
                        }
                        if(ev.Role == RoleType.Scientist && Configs.classd_insurgency_scientist_inventory.Count > 0)
                        {
                            ev.StartItems = Configs.classd_insurgency_scientist_inventory;
                        }
                        break;
                    }
            }
        }

        public void OnPlayerSetClass(SetClassEvent ev)
        {
            if(ev.Player.IsHost()) return;
            Log.Debug($"[OnPlayerSetClass] {ev.Player.GetNickname()} -> {ev.Role}");
        }

        public void OnPlayerSpawn(PlayerSpawnEvent ev)
        {
            if(ev.Player.IsHost()) return;
            Log.Debug($"[OnPlayerSpawn] {ev.Player.GetNickname()} -{ev.Role}-> {ev.Spawnpoint}");

            switch(eventmode)
            {
                case SANYA_GAME_MODE.CLASSD_INSURGENCY:
                    {
                        if(ev.Role == RoleType.ClassD)
                        {
                            ev.Spawnpoint = LCZArmoryPos;
                        }
                        break;
                    }
            }
        }

        public void OnPlayerHurt(ref PlayerHurtEvent ev)
        {
            if(ev.Player.IsHost() || ev.Player.GetRole() == RoleType.Spectator || ev.Player.characterClassManager.GodMode || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerHurt:Before] {ev.Attacker?.GetNickname()}[{ev.Attacker?.GetRole()}] -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");

            if(ev.Attacker == null) return;
            
            if(ev.DamageType != DamageTypes.Nuke
                && ev.DamageType != DamageTypes.Decont
                && ev.DamageType != DamageTypes.Wall
                && ev.DamageType != DamageTypes.Tesla)
            {
                PlayerStats.HitInfo clinfo = ev.Info;

                //GrenadeHitmark
                if(Configs.grenade_hitmark
                    && ev.DamageType == DamageTypes.Grenade
                    && ev.Player.GetUserId() != ev.Attacker.GetUserId())
                {
                    ev.Attacker.GetComponent<Scp173PlayerScript>()?.TargetHitMarker(ev.Attacker.characterClassManager.connectionToClient);
                }

                //USPMultiplier
                if(ev.DamageType == DamageTypes.Usp)
                {
                    if(ev.Player.characterClassManager.IsAnyScp())
                    {
                        clinfo.Amount *= Configs.damage_usp_multiplier_scp;
                    }
                    else
                    {
                        clinfo.Amount *= Configs.damage_usp_multiplier_human;
                    }
                }

                if(Configs.scp939_dot_damage > 0
                    && ev.DamageType == DamageTypes.Scp939
                    && ev.Player.GetUserId() != ev.Attacker.GetUserId()
                    && !Coroutines.DOTDamages.ContainsKey(ev.Player))
                {
                    Log.Debug($"[939DOT] fired {ev.Player.GetNickname()}");
                    var cor = Timing.RunCoroutine(Coroutines.DOTDamage(ev.Player, Configs.scp939_dot_damage, Configs.scp939_dot_damage_total, Configs.scp939_dot_damage_interval, DamageTypes.Scp939));
                    roundCoroutines.Add(cor);
                    Coroutines.DOTDamages.Add(ev.Player, cor);
                }

                //CuffedDivisor
                if(ev.Player.IsHandCuffed())
                {
                    clinfo.Amount /= Configs.damage_divisor_cuffed;
                }

                //SCPsDivisor
                if(ev.DamageType != DamageTypes.MicroHid)
                {
                    switch(ev.Player.GetRole())
                    {
                        case RoleType.Scp173:
                            clinfo.Amount /= Configs.damage_divisor_scp173;
                            break;
                        case RoleType.Scp106:
                            clinfo.Amount /= Configs.damage_divisor_scp106;
                            break;
                        case RoleType.Scp049:
                            clinfo.Amount /= Configs.damage_divisor_scp049;
                            break;
                        case RoleType.Scp096:
                            clinfo.Amount /= Configs.damage_divisor_scp096;
                            break;
                        case RoleType.Scp0492:
                            clinfo.Amount /= Configs.damage_divisor_scp0492;
                            break;
                        case RoleType.Scp93953:
                        case RoleType.Scp93989:
                            clinfo.Amount /= Configs.damage_divisor_scp939;
                            break;
                    }
                }

                //*****Final*****
                ev.Info = clinfo;
            }

            Log.Debug($"[OnPlayerHurt:After] {ev.Attacker?.GetNickname()}[{ev.Attacker?.GetRole()}] -{ev.Info.GetDamageName()}({ev.Info.Amount})-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");
        }

        public void OnPlayerDeath(ref PlayerDeathEvent ev)
        {
            if(ev.Player.IsHost() || ev.Player.GetRole() == RoleType.Spectator || ev.Player.characterClassManager.GodMode || ev.Player.characterClassManager.SpawnProtected) return;
            Log.Debug($"[OnPlayerDeath] {ev.Killer?.GetNickname()}[{ev.Killer?.GetRole()}] -{ev.Info.GetDamageName()}-> {ev.Player?.GetNickname()}[{ev.Player?.GetRole()}]");

            if(ev.Killer == null) return;

            if(Configs.data_enabled)
            {
                if(!string.IsNullOrEmpty(ev.Killer.GetUserId())
                    && ev.Player.GetUserId() != ev.Killer.GetUserId()
                    && PlayerDataManager.playersData.ContainsKey(ev.Killer.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Killer.GetUserId()].AddExp(Configs.level_exp_kill);
                }

                if(PlayerDataManager.playersData.ContainsKey(ev.Player.GetUserId()))
                {
                    PlayerDataManager.playersData[ev.Player.GetUserId()].AddExp(Configs.level_exp_death);
                }
            }

            if(Coroutines.DOTDamages.TryGetValue(ev.Player, out CoroutineHandle handle))
            {
                Log.Debug($"[939DOT] Removed {ev.Player.GetNickname()}");
                Timing.KillCoroutines(handle);
                Coroutines.DOTDamages.Remove(ev.Player);
            }

            if(ev.Info.GetDamageType() == DamageTypes.Scp173 && ev.Killer.GetRole() == RoleType.Scp173 && Configs.recovery_amount_scp173 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp173);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp096 && ev.Killer.GetRole() == RoleType.Scp096 && Configs.recovery_amount_scp096 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp096);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp939 && (ev.Killer.GetRole() == RoleType.Scp93953 || ev.Killer.GetRole() == RoleType.Scp93989) && Configs.recovery_amount_scp939 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp939 * 1.5f);
                ev.Info = new PlayerStats.HitInfo(ev.Info.Amount, ev.Info.Attacker, DamageTypes.RagdollLess, ev.Info.PlyId);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp0492 && ev.Killer.GetRole() == RoleType.Scp0492 && Configs.recovery_amount_scp0492 > 0)
            {
                ev.Killer.playerStats.HealHPAmount(Configs.recovery_amount_scp0492);
            }
            if(ev.Info.GetDamageType() == DamageTypes.Scp939 && Configs.scp939_dot_damage > 0 && ev.Killer.GetUserId() == ev.Player.GetUserId() && Configs.recovery_amount_scp939 > 0)
            {
                foreach(var player in Player.GetHubs())
                {
                    if(player.GetRole() == RoleType.Scp93953 || player.GetRole() == RoleType.Scp93989)
                    {
                        player.playerStats.HealHPAmount(Configs.recovery_amount_scp939);
                    }
                }
            }

            if(Configs.kill_hitmark
                && ev.Killer.GetTeam() != Team.SCP
                && !string.IsNullOrEmpty(ev.Killer.GetUserId())
                && ev.Killer.GetUserId() != ev.Player.GetUserId())
            {
                Timing.RunCoroutine(Coroutines.BigHitmark(ev.Killer.GetComponent<MicroHID>()));
            }

            if(Configs.cassie_subtitle
                && ev.Player.GetTeam() == Team.SCP
                && ev.Player.GetRole() != RoleType.Scp0492
                && ev.Player.GetRole() != RoleType.Scp079)
            {
                string fullname = CharacterClassManager._staticClasses.Get(ev.Player.GetRole()).fullName;
                string str = string.Empty;

                if(ev.Info.GetDamageType() == DamageTypes.Tesla)
                {
                    str = Subtitles.SCPDeathTesla.Replace("{0}", fullname);
                }
                else if(ev.Info.GetDamageType() == DamageTypes.Nuke)
                {
                    str = Subtitles.SCPDeathWarhead.Replace("{0}", fullname);
                }
                else if(ev.Info.GetDamageType() == DamageTypes.Decont)
                {
                    str = Subtitles.SCPDeathDecont.Replace("{0}", fullname);
                }
                else
                {
                    Team killerTeam = ev.Killer.GetTeam();
                    foreach(var i in Player.GetHubs())
                    {
                        if(i.queryProcessor.PlayerId == ev.Info.PlyId)
                        {
                            killerTeam = i.GetTeam();
                        }
                    }
                    Log.Debug($"[CheckTeam] ply:{ev.Player.queryProcessor.PlayerId} kil:{ev.Killer.queryProcessor.PlayerId} plyid:{ev.Info.PlyId} killteam:{killerTeam}");

                    if(killerTeam == Team.CDP)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "Dクラス職員").Replace("{2}", "Class-D Personnel");
                    }
                    else if(killerTeam == Team.CHI)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "カオス・インサージェンシー").Replace("{2}", "Chaos Insurgency");
                    }
                    else if(killerTeam == Team.RSC)
                    {
                        str = Subtitles.SCPDeathTerminated.Replace("{0}", fullname).Replace("{1}", "科学者").Replace("{2}", "Science Personnel");
                    }
                    else if(killerTeam == Team.MTF)
                    {
                        string unit = NineTailedFoxUnits.host.list[ev.Killer.characterClassManager.NtfUnit];
                        str = Subtitles.SCPDeathContainedMTF.Replace("{0}", fullname).Replace("{1}", unit);
                    }
                    else
                    {
                        str = Subtitles.SCPDeathUnknown.Replace("{0}", fullname);
                    }
                }

                int count = 0;
                bool isFound079 = false;
                bool isForced = false;
                foreach(var i in Player.GetHubs())
                {
                    if(ev.Player.GetUserId() == i.GetUserId()) continue;
                    if(i.GetTeam() == Team.SCP) count++;
                    if(i.GetRole() == RoleType.Scp079) isFound079 = true;
                }

                Log.Debug($"[Check079] SCPs:{count} isFound079:{isFound079} totalvol:{Generator079.mainGenerator.totalVoltage} forced:{Generator079.mainGenerator.forcedOvercharge}");
                if(count == 1
                    && isFound079
                    && Generator079.mainGenerator.totalVoltage < 4
                    && !Generator079.mainGenerator.forcedOvercharge
                    && ev.Info.GetDamageType() != DamageTypes.Nuke)
                {
                    isForced = true;
                    str = str.Replace("{-1}", "\n全てのSCPオブジェクトの安全が確保されました。SCP-079の再収用手順を開始します。\n重度収用区画は約一分後にオーバーチャージされます。").Replace("{-2}", "\nAll SCP subject has been secured. SCP-079 recontainment sequence commencing.\nHeavy containment zone will overcharge in t-minus 1 minutes.");
                }
                else
                {
                    str = str.Replace("{-1}", string.Empty).Replace("{-2}", string.Empty);
                }

                Methods.SendSubtitle(str, isForced ? 30u : 10u);
            }
        }

        public void OnPocketDimDeath(PocketDimDeathEvent ev)
        {
            Log.Debug($"[OnPocketDimDeath] {ev.Player.GetNickname()}");

            if(Configs.data_enabled)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRole() == RoleType.Scp106)
                    {
                        if(PlayerDataManager.playersData.ContainsKey(player.GetUserId()))
                        {
                            PlayerDataManager.playersData[player.GetUserId()].AddExp(Configs.level_exp_kill);
                        }
                    }
                }
            }

            if(Configs.recovery_amount_scp106 > 0)
            {
                foreach(ReferenceHub player in Player.GetHubs())
                {
                    if(player.GetRole() == RoleType.Scp106)
                    {
                        player.playerStats.HealHPAmount(Configs.recovery_amount_scp106);
                        player.GetComponent<Scp173PlayerScript>().TargetHitMarker(player.characterClassManager.connectionToClient);
                    }
                }
            }
        }

        public void OnPlayerTriggerTesla(ref TriggerTeslaEvent ev)
        {
            if(Configs.tesla_triggerable_teams.Count == 0
                || Configs.tesla_triggerable_teams.Contains((int)ev.Player.GetTeam()))
            {
                if(Configs.tesla_triggerable_disarmed || ev.Player.handcuffs.CufferId == -1)
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
            Log.Debug($"[OnPlayerDoorInteract] {ev.Player.GetNickname()}:{ev.Door.DoorName}:{ev.Door.permissionLevel}");

            if(Configs.inventory_keycard_act && ev.Player.GetTeam() != Team.SCP && !ev.Player.serverRoles.BypassMode && !ev.Door.locked)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains(ev.Door.permissionLevel))
                    {
                        ev.Allow = true;
                    }
                }
            }
        }

        public void OnPlayerLockerInteract(LockerInteractionEvent ev)
        {
            Log.Debug($"[OnPlayerLockerInteract] {ev.Player.GetNickname()}:{ev.LockerId}");
            if(Configs.inventory_keycard_act)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("PEDESTAL_ACC"))
                    {
                        ev.Allow = true;
                    }
                }
            }
        }

        public void OnTeamRespawn(ref TeamRespawnEvent ev)
        {
            Log.Debug($"[OnTeamRespawn] Queues:{ev.ToRespawn.Count} IsCI:{ev.IsChaos} MaxAmount:{ev.MaxRespawnAmt}");

            if(Configs.check_prev_spawn_team)
            {
                IsPrevSpawnChaos = ev.IsChaos;
                roundCoroutines.Add(Timing.CallDelayed(0.5f, () =>
                {
                    var mtfr = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                    Log.Debug($"[NextSpawnFixer] Original:{mtfr.nextWaveIsCI}");
                    if(mtfr.nextWaveIsCI == IsPrevSpawnChaos)
                    {
                        mtfr.nextWaveIsCI = !IsPrevSpawnChaos;
                        Log.Debug($"[NextSpawnFixer] Fixed to:{mtfr.nextWaveIsCI}");
                    }
                }));
            }

            if(Configs.stop_respawn_after_detonated && AlphaWarheadController.Host.detonated)
            {
                ev.ToRespawn.Clear();
            }

            if(Configs.godmode_after_endround && !RoundSummary.RoundInProgress())
            {
                ev.ToRespawn.Clear();
            }
        }

        public void OnGeneratorUnlock(ref GeneratorUnlockEvent ev)
        {
            Log.Debug($"[OnGeneratorUnlock] {ev.Player.GetNickname()} -> {ev.Generator.curRoom}");
            if(Configs.inventory_keycard_act && !ev.Player.serverRoles.BypassMode)
            {
                foreach(var item in ev.Player.inventory.items)
                {
                    if(ev.Player.inventory.GetItemByID(item.id).permissions.Contains("ARMORY_LVL_2"))
                    {
                        ev.Allow = true;
                    }
                }
            }

            if(ev.Allow && Configs.generator_unlock_to_open)
            {
                ev.Generator.doorAnimationCooldown = 1.5f;
                ev.Generator.NetworkisDoorOpen = true;
                ev.Generator.RpcDoSound(true);
            }
        }

        public void OnGeneratorOpen(ref GeneratorOpenEvent ev)
        {
            Log.Debug($"[OnGeneratorOpen] {ev.Player.GetNickname()} -> {ev.Generator.curRoom}");
            if(ev.Generator.prevFinish && Configs.generator_finish_to_lock) ev.Allow = false;
        }

        public void OnGeneratorClose(ref GeneratorCloseEvent ev)
        {
            Log.Debug($"[OnGeneratorClose] {ev.Player.GetNickname()} -> {ev.Generator.curRoom}");
            if(ev.Allow && ev.Generator.isTabletConnected && Configs.generator_activating_opened) ev.Allow = false;
        }

        public void OnGeneratorInsert(ref GeneratorInsertTabletEvent ev)
        {
            Log.Debug($"[OnGeneratorInsert] {ev.Player.GetNickname()} -> {ev.Generator.curRoom}");
        }

        public void OnGeneratorFinish(ref GeneratorFinishEvent ev)
        {
            Log.Debug($"[OnGeneratorFinish] {ev.Generator.curRoom}");
            if(Configs.generator_finish_to_lock) ev.Generator.NetworkisDoorOpen = false;

            int curgen = Generator079.mainGenerator.NetworktotalVoltage + 1;
            if(Configs.cassie_subtitle && !Generator079.mainGenerator.forcedOvercharge)
            {
                if(curgen < 5)
                {
                    Methods.SendSubtitle(Subtitles.GeneratorFinish.Replace("{0}", curgen.ToString()), 10);
                }
                else
                {
                    Methods.SendSubtitle(Subtitles.GeneratorComplete, 20);
                }
            }

            if(eventmode == SANYA_GAME_MODE.NIGHT && curgen >= 3 && IsEnableBlackout)
            {
                IsEnableBlackout = false;
            }
        }

        public void On914Upgrade(ref SCP914UpgradeEvent ev)
        {
            Log.Debug($"[On914Upgrade] {ev.KnobSetting} Players:{ev.Players.Count} Items:{ev.Items.Count}");

            if(Configs.scp914_intake_death)
            {
                foreach(var player in ev.Players)
                {
                    player.inventory.Clear();
                    var info = new PlayerStats.HitInfo(914914, "WORLD", DamageTypes.RagdollLess, 0);
                    player.playerStats.HurtPlayer(info, player.gameObject);
                }
            }
        }

        public void OnConsoleCommand(ConsoleCommandEvent ev)
        {
            Log.Debug($"[OnConsoleCommand] [Before] Called:{ev.Player.GetNickname()} Command:{ev.Command} Return:{ev.ReturnMessage}");

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

            Log.Debug($"[OnConsoleCommand] [After] Called:{ev.Player.GetNickname()} Command:{ev.Command} Return:{ev.ReturnMessage}");
        }

        public void OnCommand(ref RACommandEvent ev)
        {
            Log.Debug($"[OnCommand] sender:{ev.Sender.SenderId} command:{ev.Command}");

            string[] args = ev.Command.Split(' ');
            string ReturnStr = string.Empty;
            bool isSuccess = true;
            ReferenceHub player = Player.GetPlayer(ev.Sender.SenderId);

            if(args[0].ToLower() == "sanya")
            {
                if(args.Length > 1)
                {
                    switch(args[1].ToLower())
                    {
                        case "test":
                            {
                                ReturnStr = "test ok.";
                                break;
                            }
                        case "showconfig":
                            {
                                ReturnStr = Configs.GetConfigs();
                                break;
                            }
                        case "reload":
                            {
                                Plugin.Config.Reload();
                                Configs.Reload();
                                ReturnStr = "reload ok";
                                break;
                            }
                        case "cleanupdic":
                            {
                                ReturnStr = $"Ragdolls:{RagdollCleanupPatch.ragdolls.Count} Items:{ItemCleanupPatch.items.Count}";
                                break;
                            }
                        case "startair":
                            {
                                roundCoroutines.Add(Timing.RunCoroutine(Coroutines.AirSupportBomb()));
                                ReturnStr = "Started!";
                                break;
                            }
                        case "stopair":
                            {
                                ReturnStr = $"Stop ok. now:{Coroutines.isAirBombGoing}";
                                Coroutines.isAirBombGoing = false;
                                break;
                            }
                        case "106":
                            {
                                foreach(PocketDimensionTeleport pdt in UnityEngine.Object.FindObjectsOfType<PocketDimensionTeleport>())
                                {
                                    pdt.SetType(PocketDimensionTeleport.PDTeleportType.Exit);
                                }
                                ReturnStr = "All set to [Exit].";
                                break;
                            }
                        case "096":
                            {
                                foreach(var i in Player.GetHubs())
                                {
                                    if(i.GetRole() == RoleType.Scp096)
                                    {
                                        i.characterClassManager.Scp096.IncreaseRage(20f);
                                    }
                                }
                                ReturnStr = "096 enraged!";
                                break;
                            }
                        case "914":
                            {
                                if(args.Length > 2)
                                {
                                    if(!Scp914.Scp914Machine.singleton.working)
                                    {

                                        if(args[2] == "use")
                                        {
                                            Scp914.Scp914Machine.singleton.RpcActivate(NetworkTime.time);
                                            ReturnStr = $"Used : {Scp914.Scp914Machine.singleton.NetworkknobState}";
                                        }
                                        else if(args[2] == "knob")
                                        {
                                            Scp914.Scp914Machine.singleton.ChangeKnobStatus();
                                            ReturnStr = $"Knob Changed to:{Scp914.Scp914Machine.singleton.NetworkknobState}";
                                        }
                                        else
                                        {
                                            isSuccess = false;
                                            ReturnStr = "[914] Wrong Parameters.";
                                        }
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[914] SCP-914 is working now.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[914] Parameters : 914 <use/knob>";
                                }
                                break;
                            }
                        case "nukelock":
                            {
                                IsNukeLocked = !IsNukeLocked;
                                ReturnStr = $"nukelock:{IsNukeLocked}";
                                break;
                            }
                        case "sonar":
                            {
                                int counter = 0;
                                foreach(var target in Player.GetHubs())
                                {
                                    if(target.GetTeam() != player.GetTeam() && target.GetRole() != RoleType.Spectator && target.GetRole() != RoleType.None)
                                    {
                                        Methods.Target096AttackSound(target, player);
                                        counter++;
                                    }
                                }
                                ReturnStr = $"Sonar Activated : {counter}";
                                break;
                            }
                        case "blackout":
                            {
                                if(args.Length > 2 && args[2] == "hcz")
                                {
                                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, true);
                                    ReturnStr = "HCZ blackout!";
                                }
                                else
                                {
                                    Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
                                    ReturnStr = "ALL blackout!";
                                }
                                break;
                            }
                        case "explode":
                            {
                                if(args.Length > 2)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        Methods.SpawnGrenade(target.transform.position, (int)GRENADE_ID.FRAG_NADE, 0.1f, target);
                                        ReturnStr = $"success. target:{target.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[explode] missing target.";
                                    }
                                }
                                else
                                {
                                    if(player != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.FRAG_NADE, 0.1f, player);
                                        ReturnStr = $"success. target:{player.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[explode] missing target.";
                                    }
                                }
                                break;
                            }
                        case "grenade":
                            {
                                if(args.Length > 2)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.FRAG_NADE, -1, target);
                                        ReturnStr = $"success. target:{target.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[grenade] missing target.";
                                    }
                                }
                                else
                                {
                                    if(player != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.FRAG_NADE, -1, player);
                                        ReturnStr = $"success. target:{player.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[grenade] missing target.";
                                    }
                                }
                                break;
                            }
                        case "flash":
                            {
                                if(args.Length > 2)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.FLASH_NADE, -1, target);
                                        ReturnStr = $"success. target:{target.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[flash] missing target.";
                                    }
                                }
                                else
                                {
                                    if(player != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.FLASH_NADE, -1, player);
                                        ReturnStr = $"success. target:{player.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[flash] missing target.";
                                    }
                                }
                                break;
                            }
                        case "ball":
                            {
                                if(args.Length > 2)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.SCP018_NADE, -1, target);
                                        ReturnStr = $"success. target:{target.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[ball] missing target.";
                                    }
                                }
                                else
                                {
                                    if(player != null)
                                    {
                                        Methods.SpawnGrenade(player.transform.position, (int)GRENADE_ID.SCP018_NADE, -1, player);
                                        ReturnStr = $"success. target:{player.GetNickname()}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[ball] missing target.";
                                    }
                                }
                                break;
                            }
                        case "ammo":
                            {
                                player.ammoBox.Networkamount = "200:200:200";
                                ReturnStr = "Ammo set 200:200:200.";
                                break;
                            }
                        case "ev":
                            {
                                foreach(Lift lift in UnityEngine.Object.FindObjectsOfType<Lift>())
                                {
                                    lift.UseLift();
                                }
                                ReturnStr = "EV Used.";
                                break;
                            }
                        case "roompos":
                            {
                                string output = "\n";
                                foreach(var rid in UnityEngine.Object.FindObjectsOfType<Rid>())
                                {
                                    output += $"{rid.id} : {rid.transform.position}\n";
                                }
                                ReturnStr = output;
                                break;
                            }
                        case "tppos":
                            {
                                if(args.Length > 5)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        if(float.TryParse(args[3], out float x)
                                            && float.TryParse(args[4], out float y)
                                            && float.TryParse(args[5], out float z))
                                        {
                                            Vector3 pos = new Vector3(x, y, z);
                                            target.plyMovementSync.OverridePosition(pos, 0f, true);
                                            ReturnStr = $"TP to {pos}.";
                                        }
                                        else
                                        {
                                            isSuccess = false;
                                            ReturnStr = "[tppos] Wrong parameters.";
                                        }
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[tppos] missing target.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[tppos] parameters : tppos <player> <x> <y> <z>";
                                }

                                break;
                            }
                        case "pocket":
                            {
                                if(args.Length > 2)
                                {
                                    ReferenceHub target = Player.GetPlayer(args[2]);
                                    if(target != null)
                                    {
                                        target.plyMovementSync.OverridePosition(Vector3.down * 1998.5f, 0f, forceGround: true);
                                        target.effectsController.GetEffect<Corroding>("Corroding").isInPd = true;
                                        target.effectsController.EnableEffect("Corroding");
                                        ReturnStr = $"target[{target.GetNickname()}] move to PocketDimension.";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[pocket] missing target.";
                                    }
                                }
                                else
                                {
                                    if(player != null)
                                    {
                                        player.plyMovementSync.OverridePosition(Vector3.down * 1998.5f, 0f, forceGround: true);
                                        player.effectsController.GetEffect<Corroding>("Corroding").isInPd = true;
                                        player.effectsController.EnableEffect("Corroding");
                                        ReturnStr = $"target[{player.GetNickname()}] move to PocketDimension.";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[pocket] missing target.";
                                    }
                                }
                                break;
                            }
                        case "gen":
                            {
                                if(args.Length > 2)
                                {
                                    if(args[2] == "unlock")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            generator.NetworkisDoorUnlocked = true;
                                            generator.NetworkisDoorOpen = true;
                                            generator.doorAnimationCooldown = 0.5f;
                                        }
                                        ReturnStr = "gen unlocked.";
                                    }
                                    else if(args[2] == "door")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(!generator.prevFinish)
                                            {
                                                bool now = !generator.NetworkisDoorOpen;
                                                generator.NetworkisDoorOpen = now;
                                                generator.CallRpcDoSound(now);
                                            }
                                        }
                                        ReturnStr = $"gen doors interacted.";
                                    }
                                    else if(args[2] == "set")
                                    {
                                        float cur = 10f;
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(!generator.prevFinish)
                                            {
                                                generator.NetworkisDoorOpen = true;
                                                generator.NetworkisTabletConnected = true;
                                                generator.NetworkremainingPowerup = cur;
                                                cur += 10f;
                                            }
                                        }
                                        ReturnStr = "gen set.";
                                    }
                                    else if(args[2] == "once")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(!generator.prevFinish)
                                            {
                                                generator.NetworkisTabletConnected = true;
                                                generator.NetworkisDoorOpen = true;
                                                break;
                                            }
                                        }
                                        ReturnStr = "set once.";
                                    }
                                    else if(args[2] == "eject")
                                    {
                                        foreach(var generator in Generator079.generators)
                                        {
                                            if(generator.isTabletConnected)
                                            {
                                                generator.EjectTablet();
                                            }
                                        }
                                        ReturnStr = "gen ejected.";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[gen] Wrong Parameters.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[gen] Parameters : gen <unlock/door/set/once/eject>";
                                }
                                break;
                            }
                        case "spawn":
                            {
                                var mtfrespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                                if(mtfrespawn.nextWaveIsCI)
                                {
                                    mtfrespawn.timeToNextRespawn = 14f;
                                }
                                else
                                {
                                    mtfrespawn.timeToNextRespawn = 18.5f;
                                }
                                ReturnStr = $"spawn soon. nextIsCI:{mtfrespawn.nextWaveIsCI}";
                                break;
                            }
                        case "next":
                            {
                                if(args.Length > 2)
                                {
                                    MTFRespawn mtfRespawn = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                                    if(args[2] == "ci")
                                    {
                                        mtfRespawn.nextWaveIsCI = true;
                                        ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
                                    }
                                    else if(args[2] == "mtf" || args[2] == "ntf")
                                    {
                                        mtfRespawn.nextWaveIsCI = false;
                                        ReturnStr = $"set NextIsCI:{mtfRespawn.nextWaveIsCI}";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        ReturnStr = "[next] Wrong Parameters.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    ReturnStr = "[next] Wrong Parameters.";
                                }
                                break;
                            }
                        case "van":
                            {
                                PlayerManager.localPlayer.GetComponent<MTFRespawn>()?.RpcVan();
                                ReturnStr = "Van Called!";
                                break;
                            }
                        case "heli":
                            {
                                MTFRespawn mtf_r = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                                mtf_r.SummonChopper(!mtf_r.mtf_a.NetworkisLanded);
                                ReturnStr = "Heli Called!";
                                break;
                            }
                        case "now":
                            {
                                ReturnStr = TimeBehaviour.CurrentTimestamp().ToString();
                                break;
                            }
                        default:
                            {
                                ReturnStr = "Wrong Parameters.";
                                isSuccess = false;
                                break;
                            }
                    }
                    ev.Allow = false;
                    ev.Sender.RAMessage(ReturnStr, isSuccess);
                }
                else
                {
                    ev.Allow = false;
                    ev.Sender.RAMessage(string.Concat(
                        "Usage : sanya < reload / startair / stopair / nukelock / blackout ",
                        "/ roompos / tppos / pocket / gen / spawn / next / van / heli / 106 / 096 / 914 / now / ammo / cleanupdic / test >"
                        ), false);
                }
            }
        }
    }
}
