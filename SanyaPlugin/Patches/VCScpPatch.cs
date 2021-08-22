using Assets._Scripts.Dissonance;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(DissonanceUserSetup), nameof(DissonanceUserSetup.UserCode_CmdAltIsActive))]
	public static class VCScpPatch
	{
		public static void Prefix(DissonanceUserSetup __instance, bool value)
		{
			if(__instance.gameObject.TryGetComponent<CharacterClassManager>(out CharacterClassManager ccm))
				if(ccm.IsAnyScp() && (ccm.CurClass.Is939() || SanyaPlugin.Instance.Config.AltvoicechatScpsParsed.Contains(ccm.CurClass)))
					__instance.MimicAs939 = value;
		}
	}
}
