using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.CallCmdUpdateClass))]
	public static class VCTeamPatch
	{
		public static bool Prefix(Radio __instance)
		{
			if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && !string.IsNullOrEmpty(__instance.ccm.UserId) && WhiteList.Users != null && WhiteList.IsOnWhitelist(__instance.ccm.UserId)) return true;
			if(!SanyaPlugin.Instance.Config.DisableAllChat) return true;
			Log.Debug($"[VCTeamPatch] {Player.Get(__instance.ccm.gameObject).Nickname} [{__instance.ccm.CurClass}]", SanyaPlugin.Instance.Config.IsDebugged);
			__instance._dissonanceSetup.TargetUpdateForTeam(Team.RIP);
			return false;
		}
	}
}
