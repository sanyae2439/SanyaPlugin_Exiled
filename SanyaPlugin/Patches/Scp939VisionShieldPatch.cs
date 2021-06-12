using Exiled.API.Features;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.AddVision))]
	public static class Scp939VisionShieldPatch
	{
		public static void Prefix(Scp939_VisionController __instance, Scp939PlayerScript scp939)
		{
			if(SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount <= 0 || __instance._ccm.CurRole.team == Team.SCP) return;
			bool isFound = false;
			for(int i = 0; i < __instance.seeingSCPs.Count; i++)
			{
				if(__instance.seeingSCPs[i].scp == scp939)
				{
					isFound = true;
				}
			}

			if(!isFound)
			{
				Log.Debug($"[Scp939VisionShieldPatch] {scp939._hub.nicknameSync.MyNick}({scp939._hub.characterClassManager.CurClass}) -> {__instance._ccm._hub.nicknameSync.MyNick}({__instance._ccm.CurClass})", SanyaPlugin.Instance.Config.IsDebugged);
				scp939._hub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount;
				scp939._hub.playerStats.unsyncedArtificialHealth = Mathf.Clamp(scp939._hub.playerStats.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp939SeeingAhpAmount, 0, scp939._hub.playerStats.maxArtificialHealth);
			}

		}
	}
}
