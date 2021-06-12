using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp939_VisionController), nameof(Scp939_VisionController.CanSee))]
	public static class Scp939OverAllPatch
	{
		public static void Postfix(Scp939PlayerScript scp939, ref bool __result)
		{
			if(SanyaPlugin.Instance.Handlers.Overrided?.ReferenceHub.nicknameSync.Network_myNickSync == scp939._hub.nicknameSync.Network_myNickSync)
				__result = true;
		}
	}
}
