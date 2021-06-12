using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(RagdollManager), nameof(RagdollManager.SpawnRagdoll))]
	public static class PreventRagdollPatch
	{
		public static bool Prefix(PlayerStats.HitInfo ragdollInfo)
		{
			if(SanyaPlugin.Instance.Config.TeslaDeleteObjects && ragdollInfo.GetDamageType() == DamageTypes.Tesla) return false;
			else if(SanyaPlugin.Instance.Config.Scp049StackBody && ragdollInfo.GetDamageType() == DamageTypes.Scp049) return false;
			else return true;
		}
	}
}
