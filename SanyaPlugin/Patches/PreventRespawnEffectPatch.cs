using HarmonyLib;
using Respawning;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(RespawnEffectsController), nameof(RespawnEffectsController.ServerExecuteEffects))]
	public static class PreventRespawnEffectPatch
	{
		public static bool Prefix(RespawnEffectsController.EffectType type)
		{
			if(SanyaPlugin.Instance.Config.StopRespawnAfterDetonated && AlphaWarheadController.Host.detonated && type == RespawnEffectsController.EffectType.Selection) return false;
			else return true;
		}
	}
}
