using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.UserCode_CmdUpdateClass))]
	public static class VCTeamPatch
	{
		public static bool Prefix(Radio __instance)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && !string.IsNullOrEmpty(__instance._hub.characterClassManager.UserId) && WhiteList.Users != null && WhiteList.IsOnWhitelist(__instance._hub.characterClassManager.UserId)) return true;
			if(!SanyaPlugin.Instance.Config.DisableAllChat) return true;
			Log.Debug($"[VCTeamPatch] {Player.Get(__instance._hub.characterClassManager.gameObject).Nickname} [{__instance._hub.characterClassManager.CurClass}]", SanyaPlugin.Instance.Config.IsDebugged);
			__instance._dissonanceSetup.TargetUpdateForTeam(Team.RIP);
			return false;
		}
	}
}
