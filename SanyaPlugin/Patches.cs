using System;
using System.Diagnostics;
using EXILED;
using EXILED.Extensions;
using Harmony;
using Security;
using UnityEngine;

namespace SanyaPlugin
{
    [HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.StartDetonation))]
    public class StartWarheadPatch
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

    [HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.CancelDetonation), new System.Type[] { typeof(UnityEngine.GameObject) })]
    public class CancelWarheadPatch
    {
        public static bool Locked = false;

        public static bool Prefix(AlphaWarheadController __instance)
        {
            Log.Debug($"[CancelWarheadPatch] Locked:{Locked}");
            if(Locked) return false;

            if(Configs.cassie_subtitle && __instance.NetworkinProgress && __instance.timeToDetonation > 10f)
            {
                Methods.SendSubtitle(Subtitles.AlphaWarheadCancel, 7);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AlphaWarheadNukesitePanel), nameof(AlphaWarheadNukesitePanel.AllowChangeLevelState))]
    public class ChangeLeverPatch
    {
        public static bool Prefix()
        {
            Log.Debug($"[ChangeLeverPatch] Locked:{CancelWarheadPatch.Locked}");
            if(CancelWarheadPatch.Locked) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Scp049PlayerScript), nameof(Scp049PlayerScript.CallCmdRecallPlayer))]
    public class Recall049Patch
    {
        public static void Postfix(Scp049PlayerScript __instance, ref GameObject target)
        {
            Log.Debug($"[Recall049Patch] SCP049:{Player.GetPlayer(__instance.gameObject).GetNickname()} Target:{Player.GetPlayer(target).GetNickname()} TargetRole:{Player.GetPlayer(target).GetRole()}");

            if(Player.GetPlayer(target)?.GetRole() != RoleType.Spectator) return;

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
    public class IntercomTextPatch
    {
        public static bool Prefix(Intercom __instance)
        {
            try
            {
                if(!Configs.intercom_information) return true;

                int leftdecont = (int)((Math.Truncate(((11.74f * 60) * 100f)) / 100f) - (Math.Truncate(PlayerManager.localPlayer.GetComponent<DecontaminationLCZ>().time * 100f) / 100f));
                int leftautowarhead = Configs.auto_warhead_start - RoundSummary.roundTime;
                int nextRespawn = (int)Math.Truncate(PlayerManager.localPlayer.GetComponent<MTFRespawn>().timeToNextRespawn + PlayerManager.localPlayer.GetComponent<MTFRespawn>().respawnCooldown);
                string contentfix = string.Concat(
                    $"作戦経過時間 : {(RoundSummary.roundTime / 60).ToString("00")}:{(RoundSummary.roundTime % 60).ToString("00")}\n",
                    $"残存SCP : {(RoundSummary.singleton.CountTeam(Team.SCP)).ToString("00")}/{RoundSummary.singleton.classlistStart.scps_except_zombies.ToString("00")}\n",
                    $"残存Dクラス職員 : {(RoundSummary.singleton.CountTeam(Team.CDP)).ToString("00")}/{RoundSummary.singleton.classlistStart.class_ds.ToString("00")}\n",
                    $"残存科学者 : {(RoundSummary.singleton.CountTeam(Team.RSC)).ToString("00")}/{RoundSummary.singleton.classlistStart.scientists.ToString("00")}\n",
                    $"起動済み発電機 : {Generator079.mainGenerator.totalVoltage.ToString("00")}/05\n",
                    $"最下層閉鎖まで : {(leftdecont / 60).ToString("00")}:{(leftdecont % 60).ToString("00")}\n",
                    $"自動施設爆破まで : {(leftautowarhead / 60).ToString("00")}:{(leftautowarhead % 60).ToString("00")}\n",
                    $"接近中の部隊突入まで : {(nextRespawn / 60).ToString("00")}:{(nextRespawn % 60).ToString("00")}\n"
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
                        __instance._content = contentfix + "放送中... : 残り" + Mathf.CeilToInt(__instance.speechRemainingTime) + "秒";
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
            }
            catch(Exception e)
            {
                Log.Error($"[IntercomTextPatch] {e}");
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(DecontaminationLCZ), nameof(DecontaminationLCZ.RpcPlayAnnouncement))]
    public class DecontAnnouncePatch
    {
        public static bool Prefix(DecontaminationLCZ __instance, ref int id, ref bool global)
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

    [HarmonyPatch(typeof(NineTailedFoxUnits),nameof(NineTailedFoxUnits.AddUnit))]
    public class NTFUnitPatch
    {
        public static void Postfix(NineTailedFoxUnits __instance, ref string unit)
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
                    Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}",unit).Replace("{1}",SCPCount.ToString()), 30);
                }
                else
                {
                    Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", unit), 30);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Scp096PlayerScript),nameof(Scp096PlayerScript.ProcessLooking))]
    public class Scp096LookingPatch
    {
        public static bool Prefix(Scp096PlayerScript __instance)
        {
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

    [HarmonyPatch(typeof(RateLimit),nameof(RateLimit.CanExecute))]
    public class RateLimitPatch
    {
        public static void Postfix(RateLimit __instance, ref bool __result)
        {
            if(__result == false)
            {
                Log.Debug($"[RateLimitPatch] {__instance._usagesAllowed}:{__instance._timeWindow}");
            }
        }
    }

    //[HarmonyPatch(typeof(DissonanceUserSetup),nameof(DissonanceUserSetup.CallCmdAltIsActive))]
    //public class VCPatch
    //{
    //    public static void Prefix(DissonanceUserSetup __instance, bool value)
    //    {

    //    }
    //}
}