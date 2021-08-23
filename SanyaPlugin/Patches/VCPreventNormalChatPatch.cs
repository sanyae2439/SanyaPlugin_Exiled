using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.UserCode_CmdSyncVoiceChatStatus))]
	public static class VCPreventNormalChatPatch
	{
		public static bool Prefix(Radio __instance, ref bool b)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist 
				&& WhiteList.Users != null 
				&& !string.IsNullOrEmpty(__instance._hub.characterClassManager.UserId) 
				&& WhiteList.IsOnWhitelist(__instance._hub.characterClassManager.UserId)) 
				return true;
			if(SanyaPlugin.Instance.Config.DisableAllChat) 
				return false;
			Log.Debug($"[VCPreventNormalChatPatch] team:{__instance._hub.characterClassManager.CurRole.team} value:{b} current:{__instance._syncPrimaryVoicechatButton} RoundEnded:{RoundSummary.singleton.RoundEnded}", SanyaPlugin.Instance.Config.IsDebugged);
			return true;
		}
	}
}