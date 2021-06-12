using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdSyncVoiceChatStatus))]
	public static class VCPreventsPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.Users != null && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(SanyaPlugin.Instance.Config.DisableAllChat) return false;
			if(!SanyaPlugin.Instance.Config.DisableSpectatorChat || (SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(__instance.ccm.UserId))) return true;
			var team = __instance.ccm.Classes.SafeGet(__instance.ccm.CurClass).team;
			Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance.isVoiceChatting} RoundEnded:{RoundSummary.singleton._roundEnded}", SanyaPlugin.Instance.Config.IsDebugged);
			if(SanyaPlugin.Instance.Config.DisableSpectatorChat && team == Team.RIP && !RoundSummary.singleton._roundEnded) b = false;
			return true;
		}
	}
}
