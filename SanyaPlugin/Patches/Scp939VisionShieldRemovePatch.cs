using Exiled.API.Features;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.UpdateVisions))]
	public static class Scp939VisionShieldRemovePatch
	{
		public static bool Prefix(Scp939_VisionController __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount < 0) return true;

			for(int i = 0; i < __instance.seeingSCPs.Count; i++)
			{
				__instance.seeingSCPs[i].remainingTime -= 0.02f;
				if(__instance.seeingSCPs[i].scp == null || !__instance.seeingSCPs[i].scp.iAm939 || __instance.seeingSCPs[i].remainingTime <= 0f)
				{
					if(__instance.seeingSCPs[i].scp != null && __instance.seeingSCPs[i].scp.iAm939 && __instance._ccm.CurRole.team != Team.SCP)
					{
						Log.Debug($"[Scp939VisionShieldRemovePatch] {__instance.seeingSCPs[i].scp._hub.nicknameSync.MyNick}({__instance.seeingSCPs[i].scp._hub.characterClassManager.CurClass}) -> {__instance._ccm._hub.nicknameSync.MyNick}({__instance._ccm.CurClass})", SanyaPlugin.Instance.Config.IsDebugged);
						__instance.seeingSCPs[i].scp._hub.playerStats.NetworkmaxArtificialHealth = Mathf.Clamp(__instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth - SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, __instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth);
						__instance.seeingSCPs[i].scp._hub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(__instance.seeingSCPs[i].scp._hub.playerStats.unsyncedArtificialHealth - SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, __instance.seeingSCPs[i].scp._hub.playerStats.maxArtificialHealth);
					}
					__instance.seeingSCPs.RemoveAt(i);
					return false;
				}
			}
			return false;
		}
	}
}
