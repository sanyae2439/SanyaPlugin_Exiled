using Exiled.API.Features;
using HarmonyLib;
using Scp2536;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(Scp2536Controller), nameof(Scp2536Controller.TreebugLog))]
	public static class Debug2536Patch
	{
		public static void Postfix(string cont) => Log.Debug($"[Debug2536] {cont}", SanyaPlugin.Instance.Config.IsDebugged);
	}
}
