using Assets._Scripts.Dissonance;
using EXILED;
using Harmony;
using UnityEngine;

namespace SanyaPlugin
{
    [HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.CancelDetonation), new System.Type[] { typeof(UnityEngine.GameObject) })]
    public class CancelWarheadPatch
    {
        public static bool Locked = false;

        public static bool Prefix()
        {
            Plugin.Debug($"[Patch.CancelDetonation] Locked:{Locked}");
            if(Locked) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(AlphaWarheadNukesitePanel), nameof(AlphaWarheadNukesitePanel.AllowChangeLevelState))]
    public class ChangeLeverPatch
    {
        public static bool Prefix()
        {
            Plugin.Debug($"[Patch.ChangeLeverPatch] Locked:{CancelWarheadPatch.Locked}");
            if(CancelWarheadPatch.Locked) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Scp049PlayerScript), nameof(Scp049PlayerScript.CallCmdRecallPlayer))]
    public class Recall049Patch
    {
        public static void Postfix(Scp049PlayerScript __instance, ref GameObject target)
        {
            Plugin.Debug($"[Patch.Recall049Patch] SCP049:{ReferenceHub.GetHub(__instance.gameObject).GetName()} Target:{ReferenceHub.GetHub(target).GetName()}");
            if(SanyaPluginConfig.recovery_amount_scp049 > 0)
            {
                ReferenceHub.GetHub(__instance.gameObject).playerStats.HealHPAmount(SanyaPluginConfig.recovery_amount_scp049);
            }
            if(SanyaPluginConfig.scp049_reset_ragdoll_after_recall)
            {
                foreach(var player in Plugin.GetHubs())
                {
                    __instance.RpcSetDeathTime(player.gameObject);
                }
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
