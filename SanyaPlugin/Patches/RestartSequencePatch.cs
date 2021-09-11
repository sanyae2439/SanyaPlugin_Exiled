using HarmonyLib;
using Respawning;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(RespawnManager), nameof(RespawnManager.RestartSequence))]
	public static class RestartSequencePatch
	{
		public static void Postfix(RespawnManager __instance)
		{
			if(SanyaPlugin.Instance.Config.TimeToRespawnAfterDetonated != -1 && AlphaWarheadController.Host.detonated)
				__instance._timeForNextSequence = SanyaPlugin.Instance.Config.TimeToRespawnAfterDetonated;
		}
	}
}
