using System;
using System.Collections.Generic;
using UnityEngine;
using Assets._Scripts.Dissonance;
using Security;
using Mirror;
using Harmony;
using MEC;
using EXILED;
using EXILED.Extensions;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;

namespace SanyaPlugin.Patches
{
    [HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.StartDetonation))]
    public static class StartWarheadPatch
    {
        public static void Postfix(AlphaWarheadController __instance)
        {
            Log.Debug($"[StartWarheadPatch] inprogess:{__instance.NetworkinProgress}");
            if(Configs.cassie_subtitle && __instance.NetworkinProgress)
            {
                bool isresumed = AlphaWarheadController._resumeScenario != -1;
                double left = isresumed ? __instance.timeToDetonation : __instance.timeToDetonation - 4;
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
        }
    }

    [HarmonyPatch(typeof(AlphaWarheadNukesitePanel), nameof(AlphaWarheadNukesitePanel.AllowChangeLevelState))]
    public static class ChangeLeverPatch
    {
        public static bool Prefix()
        {
            Log.Debug($"[ChangeLeverPatch] Locked:{EventHandlers.IsNukeLocked}");
            if(EventHandlers.IsNukeLocked)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(Door), nameof(Door.OpenWarhead))]
    public static class DoorWarheadPatch
    {
        public static bool Prefix(Door __instance, bool force, bool lockDoor)
        {
            if(!Configs.fix_doors_on_countdown) return true;

            if(EventHandlers.autowarheadstarted)
            {
                force = true;
                lockDoor = true;
            }

            if(__instance.permissionLevel == "UNACCESSIBLE" || (__instance.dontOpenOnWarhead && !force))
            {
                return false;
            }
            if(lockDoor)
            {
                __instance.warheadlock = true;
            }
            if(!__instance.locked || force)
            {
                if(!__instance.isOpen)
                {
                    __instance.RpcDoSound();
                }
                __instance.moving.moving = true;
                __instance.SetState(open: true);
                __instance.UpdateLock();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Scp049PlayerScript), nameof(Scp049PlayerScript.CallCmdRecallPlayer))]
    public static class Recall049Patch
    {
        public static void Postfix(Scp049PlayerScript __instance, ref GameObject target)
        {
            Log.Debug($"[Recall049Patch] SCP049:{Player.GetPlayer(__instance.gameObject).GetNickname()} Target:{Player.GetPlayer(target).GetNickname()} TargetRole:{Player.GetPlayer(target).GetRole()}");

            if(Player.GetPlayer(target)?.GetRole() != RoleType.Scp0492) return;

            if(Configs.recovery_amount_scp049 > 0)
            {
                ReferenceHub.GetHub(__instance.gameObject).playerStats.HealHPAmount(Configs.recovery_amount_scp049);
            }
            if(Configs.scp049_reset_ragdoll_after_recall)
            {
                foreach(var player in Player.GetHubs())
                {
                    __instance.RpcSetDeathTime(player.gameObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
    public static class IntercomTextPatch
    {
        public static bool Prefix(Intercom __instance)
        {
            if(!Configs.intercom_information) return true;

            int leftdecont = (int)((Math.Truncate(((11.74f * 60) * 100f)) / 100f) - (Math.Truncate(PlayerManager.localPlayer.GetComponent<DecontaminationLCZ>().time * 100f) / 100f));
            int leftautowarhead = Mathf.Clamp(Configs.auto_warhead_start - RoundSummary.roundTime, 0, Configs.auto_warhead_start);
            int nextRespawn = (int)Math.Truncate(PlayerManager.localPlayer.GetComponent<MTFRespawn>().timeToNextRespawn + PlayerManager.localPlayer.GetComponent<MTFRespawn>().respawnCooldown);
            bool isContain = PlayerManager.localPlayer.GetComponent<CharacterClassManager>()._lureSpj.NetworkallowContain;
            bool isAlreadyUsed = UnityEngine.Object.FindObjectOfType<OneOhSixContainer>().Networkused;

            float totalvoltagefloat = 0f;
            foreach(var i in Generator079.generators)
            {
                totalvoltagefloat += i.localVoltage;
            }
            totalvoltagefloat *= 1000f;

            string contentfix = string.Concat(
                $"作戦経過時間 : {RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}\n",
                $"残存SCPオブジェクト : {RoundSummary.singleton.CountTeam(Team.SCP):00}/{RoundSummary.singleton.classlistStart.scps_except_zombies:00}\n",
                $"残存Dクラス職員 : {RoundSummary.singleton.CountTeam(Team.CDP):00}/{RoundSummary.singleton.classlistStart.class_ds:00}\n",
                $"残存科学者 : {RoundSummary.singleton.CountTeam(Team.RSC):00}/{RoundSummary.singleton.classlistStart.scientists:00}\n",
                $"施設内余剰電力 : {totalvoltagefloat:0000}kVA\n",
                $"AlphaWarheadのステータス : {(AlphaWarheadOutsitePanel.nukeside.Networkenabled ? "READY" : "DISABLED")}\n",
                $"SCP-106再収用設備：{(isContain ? (isAlreadyUsed ? "使用済み" : "準備完了") : "人員不在")}\n",
                $"軽度収用区画閉鎖まで : {leftdecont / 60:00}:{leftdecont % 60:00}\n",
                $"自動施設爆破開始まで : {leftautowarhead / 60:00}:{leftautowarhead % 60:00}\n",
                $"接近中の部隊突入まで : {nextRespawn / 60:00}:{nextRespawn % 60:00}\n"
                );


            if(__instance.Muted)
            {
                __instance._content = contentfix + "あなたは管理者によってミュートされている";
            }
            else if(Intercom.AdminSpeaking)
            {
                __instance._content = contentfix + "管理者が放送設備をオーバーライド中";
            }
            else if(__instance.remainingCooldown > 0f)
            {
                __instance._content = contentfix + "放送設備再起動中 : " + Mathf.CeilToInt(__instance.remainingCooldown) + "秒必要";
            }
            else if(__instance.Networkspeaker != null)
            {
                if(__instance.speechRemainingTime == -77f)
                {
                    __instance._content = contentfix + "放送中... : オーバーライド";
                }
                else
                {
                    __instance._content = contentfix + $"{ReferenceHub.GetHub(__instance.Networkspeaker).GetNickname()}が放送中... : 残り" + Mathf.CeilToInt(__instance.speechRemainingTime) + "秒";
                }
            }
            else
            {
                __instance._content = contentfix + "放送設備準備完了";
            }
            if(__instance._contentDirty)
            {
                __instance.NetworkintercomText = __instance._content;
                __instance._contentDirty = false;
            }
            if(Intercom.AdminSpeaking != Intercom.LastState)
            {
                Intercom.LastState = Intercom.AdminSpeaking;
                __instance.RpcUpdateAdminStatus(Intercom.AdminSpeaking);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(DecontaminationLCZ), nameof(DecontaminationLCZ.RpcPlayAnnouncement))]
    public static class DecontAnnouncePatch
    {
        public static bool Prefix(ref int id, ref bool global)
        {
            Log.Debug($"[DecontAnnouncePatch] id:{id} global:{global}");
            if(Configs.cassie_subtitle)
            {
                global = true;
                switch(id)
                {
                    case 0:
                        {
                            Methods.SendSubtitle(Subtitles.DecontaminationInit, 20);
                            break;
                        }
                    case 1:
                        {
                            Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "10"), 15);
                            break;
                        }
                    case 2:
                        {
                            Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "5"), 15);
                            break;
                        }
                    case 3:
                        {
                            Methods.SendSubtitle(Subtitles.DecontaminationMinutesCount.Replace("{0}", "1"), 15);
                            break;
                        }
                    case 4:
                        {
                            Methods.SendSubtitle(Subtitles.Decontamination30s, 45);
                            break;
                        }
                    case 5:
                        {
                            Methods.SendSubtitle(Subtitles.DecontaminationLockdown, 15);
                            break;
                        }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(DecontaminationLCZ), nameof(DecontaminationLCZ.DoServersideStuff))]
    public static class DecontStopDelayPatch
    {
        public static bool Prefix(DecontaminationLCZ __instance)
        {
            if(!NetworkServer.active || __instance._curAnm >= __instance.announcements.Count || !__instance._ccm.RoundStarted)
            {
                return false;
            }
            __instance.time += Time.deltaTime;
            if(__instance.time / 60f > __instance.announcements[__instance._curAnm].startTime)
            {
                __instance.RpcPlayAnnouncement(__instance._curAnm, __instance.GetOption("global", __instance._curAnm));
                if(__instance.GetOption("checkpoints", __instance._curAnm))
                {
                    __instance.Invoke("CallOpenDoors", 10f);
                }
                __instance._curAnm++;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(DecontaminationSpeaker), nameof(DecontaminationSpeaker.OpenDoors))]
    public static class DecontOpenWhenCountdownPatch
    {
        public static bool Prefix()
        {
            if(!Configs.fix_doors_on_countdown_decont) return true;

            foreach(Door door in DecontaminationSpeaker.singleton.doorsToOpen)
            {
                if(door.curCooldown <= 0f && !door.isOpen)
                {
                    door.OpenDecontamination();
                }
            }

            foreach(var door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if(!(door.permissionLevel == "UNACCESSIBLE")
                    && !door.dontOpenOnWarhead
                    && !(door.transform.position.y < -100f)
                    && !(door.transform.position.y > 100f))
                {
                    door.decontlock = true;
                    if(!door.isOpen)
                    {
                        door.RpcDoSound();
                    }
                    door.moving.moving = true;
                    door.SetState(open: true);
                    door.UpdateLock();
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(NineTailedFoxUnits), nameof(NineTailedFoxUnits.AddUnit))]
    public static class NTFUnitPatch
    {
        public static void Postfix(ref string unit)
        {
            Log.Debug($"[NTFUnitPatch] unit:{unit}");

            if(Configs.cassie_subtitle && RoundSummary.roundTime > 2)
            {
                int SCPCount = 0;
                foreach(var i in Player.GetHubs())
                {
                    if(i.GetTeam() == Team.SCP && i.GetRole() != RoleType.Scp0492)
                    {
                        SCPCount++;
                    }
                }

                if(SCPCount > 0)
                {
                    Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}", unit).Replace("{1}", SCPCount.ToString()), 30);
                }
                else
                {
                    Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", unit), 30);
                }
            }
        }
    }

    [HarmonyPatch(typeof(MTFRespawn), nameof(MTFRespawn.SummonChopper))]
    public static class StopChopperAfterDetonatedPatch
    {
        public static bool Prefix()
        {
            if(Configs.stop_respawn_after_detonated && AlphaWarheadController.Host.detonated) return false;
            else return true;
        }
    }

    [HarmonyPatch(typeof(RagdollManager), nameof(RagdollManager.SpawnRagdoll))]
    public static class RagdollCleanupPatch
    {
        public static Dictionary<GameObject, float> ragdolls = new Dictionary<GameObject, float>();

        public static bool Prefix(RagdollManager __instance, Vector3 pos, Quaternion rot, int classId, PlayerStats.HitInfo ragdollInfo, bool allowRecall, string ownerID, string ownerNick, int playerId)
        {
            if(Configs.ragdoll_cleanup < 0) return true;

            Log.Debug($"[RagdollCleanupPatch] {Enum.Parse(typeof(RoleType), classId.ToString())}{pos} Time:{Time.time} Cleanuptimes:{Configs.ragdoll_cleanup}");

            Role role = __instance.ccm.Classes.SafeGet(classId);
            if(role.model_ragdoll != null)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(role.model_ragdoll, pos + role.ragdoll_offset.position, Quaternion.Euler(rot.eulerAngles + role.ragdoll_offset.rotation));
                NetworkServer.Spawn(gameObject);
                gameObject.GetComponent<Ragdoll>().Networkowner = new Ragdoll.Info(ownerID, ownerNick, ragdollInfo, role, playerId);
                gameObject.GetComponent<Ragdoll>().NetworkallowRecall = allowRecall;
                ragdolls.Add(gameObject, Time.time);
            }
            if(ragdollInfo.GetDamageType().isScp || ragdollInfo.GetDamageType() == DamageTypes.Pocket)
            {
                __instance.RegisterScpFrag();
            }
            else if(ragdollInfo.GetDamageType() == DamageTypes.Grenade)
            {
                RoundSummary.kills_by_frag++;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetPickup))]
    public static class ItemCleanupPatch
    {
        public static Dictionary<GameObject, float> items = new Dictionary<GameObject, float>();

        public static bool Prefix(Inventory __instance, ref Pickup __result, ItemType droppedItemId, float dur, Vector3 pos, Quaternion rot, int s, int b, int o)
        {
            if(Configs.item_cleanup < 0 || __instance.name == "Host") return true;

            Log.Debug($"[ItemCleanupPatch] {droppedItemId}{pos} Time:{Time.time} Cleanuptimes:{Configs.item_cleanup}");

            if(droppedItemId < ItemType.KeycardJanitor)
            {
                __result = null;
                return false;
            }
            GameObject gameObject = UnityEngine.Object.Instantiate(__instance.pickupPrefab);
            NetworkServer.Spawn(gameObject);
            items.Add(gameObject, Time.time);
            gameObject.GetComponent<Pickup>().SetupPickup(new Pickup.PickupInfo
            {
                position = pos,
                rotation = rot,
                itemId = droppedItemId,
                durability = dur,
                weaponMods = new int[3]
                {
                    s,
                    b,
                    o
                },
                ownerPlayer = __instance.gameObject
            });
            __result = gameObject.GetComponent<Pickup>();

            return false;
        }
    }

    [HarmonyPatch(typeof(Scp096PlayerScript), nameof(Scp096PlayerScript.ProcessLooking))]
    public static class Scp096LookingPatch
    {
        public static bool Prefix(Scp096PlayerScript __instance)
        {
            if(!Configs.scp096_high_sensitive) return true;

            foreach(var player in PlayerManager.players)
            {
                ReferenceHub hubs = ReferenceHub.GetHub(player);
                if(!hubs.characterClassManager.Scp173.SameClass
                    && hubs.characterClassManager.Scp173.LookFor173(__instance.gameObject, true)
                    && __instance._ccm.Scp173.LookFor173(player, false))
                {
                    __instance.IncreaseRage(Time.fixedDeltaTime);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ConsumableAndWearableItems), nameof(ConsumableAndWearableItems.RpcSetCooldown))]
    public static class MedicalUsedPatch
    {
        public static void Postfix(ConsumableAndWearableItems __instance, int mid)
        {
            ReferenceHub player = ReferenceHub.GetHub(__instance.gameObject);
            ItemType itemtype = __instance.usableItems[mid].inventoryID;
            Log.Debug($"[MedicalUsedPatch] {player?.GetNickname()} mid:{mid}/{itemtype}");

            if(itemtype == ItemType.Medkit || itemtype == ItemType.SCP500)
            {
                if(Coroutines.DOTDamages.TryGetValue(player, out CoroutineHandle handle))
                {
                    Log.Debug($"[939DOT] Removed {player.GetNickname()}");
                    Timing.KillCoroutines(handle);
                    Coroutines.DOTDamages.Remove(player);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AmmoBox), nameof(AmmoBox.SetAmmoAmount))]
    public static class AmmoPatch
    {
        public static bool Prefix(AmmoBox __instance)
        {
            int[] ammoTypes = __instance._ccm.Classes.SafeGet(__instance._ccm.CurClass).ammoTypes;

            switch(EventHandlers.eventmode)
            {
                case SANYA_GAME_MODE.CLASSD_INSURGENCY:
                    {
                        if(__instance._ccm.CurClass == RoleType.ClassD && Configs.classd_insurgency_classd_ammo.Count > 0)
                        {
                            ammoTypes = Configs.classd_insurgency_classd_ammo.ToArray();
                        }
                        break;
                    }
            }

            __instance.Networkamount = string.Concat(new object[]
            {
            ammoTypes[0],
            ":",
            ammoTypes[1],
            ":",
            ammoTypes[2]
            });

            return false;
        }
    }

    [HarmonyPatch(typeof(RateLimit), nameof(RateLimit.CanExecute))]
    public static class RateLimitPatch
    {
        public static void Postfix(RateLimit __instance, ref bool __result)
        {
            if(__result == false)
            {
                Log.Debug($"[RateLimitPatch] {__instance._usagesAllowed}:{__instance._timeWindow}");
            }
        }
    }

    [HarmonyPatch(typeof(Grenades.Grenade), "set_NetworkthrowerTeam")]
    public static class GrenadeThrowerPatch
    {
        public static List<GameObject> instantFusePlayers = new List<GameObject>();

        public static void Prefix(Grenades.Grenade __instance, ref Team value)
        {
            Log.Debug($"[GrenadeThrowerPatch] value:{value} isscp018:{__instance is Grenades.Scp018Grenade}");
            if(Configs.scp018_friendly_fire && __instance is Grenades.Scp018Grenade) value = Team.TUT;
        }
    }

    [HarmonyPatch(typeof(Grenades.Grenade), nameof(Grenades.Grenade.ServersideExplosion))]
    public static class GrenadeLogPatch
    {
        public static bool Prefix(Grenades.Grenade __instance, ref bool __result)
        {
            try
            {
                if(__instance.thrower?.name != "Host")
                {
                    string text = (__instance.thrower != null) ? (__instance.thrower.ccm.UserId + " (" + __instance.thrower.nick.MyNick + ")") : "(UNKNOWN)";
                    ServerLogs.AddLog(ServerLogs.Modules.Logger, "Player " + text + "'s " + __instance.logName + " grenade exploded.", ServerLogs.ServerLogType.GameEvent);
                }
                __result = true;
                return false;
            }
            catch(Exception)
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Grenades.Scp018Grenade), nameof(Grenades.Scp018Grenade.OnSpeedCollisionEnter))]
    public static class Scp018Patch
    {
        public static bool Prefix(Grenades.Scp018Grenade __instance, Collision collision, float relativeSpeed)
        {
            //__instance.damageHurt = 0.95f
            //__instance.damageScpMultiplier = 4.85f

            Vector3 velocity = __instance.rb.velocity * __instance.bounceSpeedMultiplier;
            float num = __instance.topSpeedPerBounce[__instance.bounce];
            if(relativeSpeed > num)
            {
                __instance.rb.velocity = velocity.normalized * num;
                if(__instance.actionAllowed)
                {
                    __instance.bounce = Mathf.Min(__instance.bounce + 1, __instance.topSpeedPerBounce.Length - 1);
                }
            }
            else
            {
                if(relativeSpeed > __instance.source.maxDistance)
                {
                    __instance.source.maxDistance = relativeSpeed;
                }
                __instance.rb.velocity = velocity;
            }
            if(NetworkServer.active)
            {
                Collider collider = collision.collider;
                int num2 = 1 << collider.gameObject.layer;
                if(num2 == __instance.layerGlass)
                {
                    if(__instance.actionAllowed && relativeSpeed >= __instance.breakpointGlass)
                    {
                        __instance.cooldown = __instance.cooldownGlass;
                        BreakableWindow component = collider.GetComponent<BreakableWindow>();
                        if(component != null)
                        {
                            component.ServerDamageWindow(relativeSpeed * __instance.damageGlass);
                        }
                    }
                }
                else if(num2 == __instance.layerDoor)
                {
                    if(relativeSpeed >= __instance.breakpointDoor)
                    {
                        __instance.cooldown = __instance.cooldownDoor;
                        Door componentInParent = collider.GetComponentInParent<Door>();
                        if(componentInParent != null && !componentInParent.GrenadesResistant)
                        {
                            componentInParent.DestroyDoor(b: true);
                        }
                    }
                }
                else if((num2 == __instance.layerHitbox || num2 == __instance.layerIgnoreRaycast) && __instance.actionAllowed && relativeSpeed >= __instance.breakpointHurt)
                {
                    __instance.cooldown = __instance.cooldownHurt;
                    ReferenceHub componentInParent2 = collider.GetComponentInParent<ReferenceHub>();
                    if(componentInParent2 != null && (ServerConsole.FriendlyFire || componentInParent2.gameObject == __instance.thrower.gameObject || componentInParent2.weaponManager.GetShootPermission(__instance.throwerTeam)))
                    {
                        float num3 = relativeSpeed * __instance.damageHurt * Configs.scp018_damage_multiplier;

                        //componentInParent2.playerStats.ccm.CurClass != RoleType.Scp106 && 
                        if(componentInParent2.playerStats.ccm.Classes.SafeGet(componentInParent2.playerStats.ccm.CurClass).team == Team.SCP)
                        {
                            num3 *= __instance.damageScpMultiplier;
                        }

                        componentInParent2.playerStats.HurtPlayer(new PlayerStats.HitInfo(num3, __instance.logName, DamageTypes.Grenade, __instance.throwerGameObject.GetPlayer().GetPlayerId()), componentInParent2.playerStats.gameObject);
                    }
                }
                if(__instance.bounce >= __instance.topSpeedPerBounce.Length - 1 && relativeSpeed >= num && !__instance.hasHitMaxSpeed)
                {
                    __instance.NetworkfuseTime = NetworkTime.time + 10.0;
                    __instance.hasHitMaxSpeed = true;
                }
            }
            //base.OnSpeedCollisionEnter(collision, relativeSpeed);
            return false;
        }
    }

    [HarmonyPatch(typeof(CheaterReport), nameof(CheaterReport.CallCmdReport))]
    public static class ReportPatch
    {
        public static bool Prefix(CheaterReport __instance, int playerId, string reason)
        {
            ReferenceHub reported = Player.GetPlayer(playerId);
            ReferenceHub reporter = Player.GetPlayer(__instance.gameObject);
            Log.Debug($"[ReportPatch] Reported:{reported.GetNickname()} Reason:{reason} Reporter:{reporter.GetNickname()}");

            if(!string.IsNullOrEmpty(Configs.report_webhook)
                && !string.IsNullOrEmpty(reporter.GetUserId())
                && !string.IsNullOrEmpty(reported.GetUserId())
                && reported.GetPlayerId() != reporter.GetPlayerId())
            {
                Methods.SendReport(reported, reason, reporter);
                __instance.GetComponent<GameConsoleTransmission>().SendToClient(__instance.connectionToClient, "Player report successfully sent.", "green");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(DissonanceUserSetup), nameof(DissonanceUserSetup.CallCmdAltIsActive))]
    public static class VCPatch
    {
        public static void Prefix(DissonanceUserSetup __instance, bool value)
        {
            if(!Configs.scp_can_talk_to_humans) return;

            CharacterClassManager ccm = __instance.gameObject.GetComponent<CharacterClassManager>();
            if(ccm.IsAnyScp() && ccm.CurClass != RoleType.Scp079)
            {
                __instance.MimicAs939 = value;
            }

            return;
        }
    }

    [HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdSyncVoiceChatStatus))]
    public static class VCPreventsPatch
    {
        public static bool Prefix(Radio __instance, ref bool b)
        {
            if(Configs.disable_all_chat) return false;
            if(!Configs.disable_spectator_chat) return true;
            var team = __instance.ccm.Classes.SafeGet(__instance.ccm.CurClass).team;
            Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance.isVoiceChatting}");
            if(Configs.disable_spectator_chat && team == Team.RIP) b = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdUpdateClass))]
    public static class VCTeamPatch
    {
        public static bool Prefix(Radio __instance)
        {
            if(!Configs.disable_all_chat) return true;
            Log.Debug($"[VCTeamPatch] {__instance.ccm.gameObject.GetPlayer().GetNickname()} [{__instance.ccm.CurClass}]");
            __instance._dissonanceSetup.TargetUpdateForTeam(Team.RIP);
            return false;
        }
    }
}