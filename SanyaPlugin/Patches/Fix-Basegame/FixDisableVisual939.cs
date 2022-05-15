using CustomPlayerEffects;
using HarmonyLib;
using Mirror;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(Visuals939),nameof(Visuals939.Disabled))]
	public static class FixDisableVisual939
	{
		public static bool Prefix(Visuals939 __instance)
		{
			if(NetworkServer.active && __instance != null)
				Visuals939.EnabledEffects.Remove(__instance);
			return false;
		}
	}
}
