using Assets._Scripts.Dissonance;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Radio), nameof(Radio.UserCode_CmdSyncTransmissionStatus))]
	public static class VCScpPatch
	{
		public static void Prefix(Radio __instance, bool b)
		{
			if(__instance._hub.characterClassManager.IsAnyScp() && (__instance._hub.characterClassManager.CurClass.Is939() 
				|| SanyaPlugin.Instance.Config.AltvoicechatScpsParsed.Contains(__instance._hub.characterClassManager.CurClass)))
				__instance._dissonanceSetup.MimicAs939 = b;
		}
	}
}
