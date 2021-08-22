using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.UserCode_CmdSyncVoiceChatStatus))]
	public static class VCPreventsPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.Users != null && !string.IsNullOrEmpty(__instance._hub.characterClassManager.UserId) && WhiteList.IsOnWhitelist(__instance._hub.characterClassManager.UserId)) return true;
			if(SanyaPlugin.Instance.Config.DisableAllChat) return false;
			if(!SanyaPlugin.Instance.Config.DisableSpectatorChat || (SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.IsOnWhitelist(__instance._hub.characterClassManager.UserId))) return true;
			var team = __instance._hub.characterClassManager.Classes.SafeGet(__instance._hub.characterClassManager.CurClass).team;
			Log.Debug($"[VCPreventsPatch] team:{team} value:{b} current:{__instance._syncPrimaryVoicechatButton} RoundEnded:{RoundSummary.singleton.RoundEnded}", SanyaPlugin.Instance.Config.IsDebugged);
			if(SanyaPlugin.Instance.Config.DisableSpectatorChat && team == Team.RIP && !RoundSummary.singleton.RoundEnded) b = false;
			return true;
		}
	}
}
