using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.PreWindup))]
	public static class FastEnragePatch
	{
		public static void Prefix(ref float delay) => delay = 0.1f;
	}
}