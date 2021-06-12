using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(AmbientSoundPlayer), nameof(AmbientSoundPlayer.GenerateRandom))]
	public static class PreventAmbientSoundPatch
	{
		public static bool Prefix() => RoundSummary.RoundInProgress();
	}
}
