using CustomPlayerEffects;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.CanSee))]
	public static class Scp939OverAllPatch
	{
		public static void Postfix(Visuals939 scp939, ref bool __result)
		{
			if(scp939.IsEnabled && SanyaPlugin.Instance.Handlers.Overrided?.ReferenceHub.nicknameSync.MyNick == scp939.Hub.nicknameSync.MyNick)
				__result = true;
		}
	}
}
