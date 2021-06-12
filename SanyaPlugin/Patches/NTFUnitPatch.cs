using Exiled.API.Features;
using HarmonyLib;
using MapGeneration;
using Respawning.NamingRules;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(UnitNamingRule), nameof(UnitNamingRule.AddCombination))]
	public static class NTFUnitPatch
	{
		public static void Postfix(ref string regular)
		{
			if(PlayerManager.localPlayer == null || SeedSynchronizer.Seed == 0) return;
			Log.Debug($"[NTFUnitPatch] unit:{regular}", SanyaPlugin.Instance.Config.IsDebugged);

			if(SanyaPlugin.Instance.Config.CassieSubtitle)
			{
				int SCPCount = 0;

				foreach(var i in Player.List)
					if(i.Team == Team.SCP && i.Role != RoleType.Scp0492)
						SCPCount++;

				if(SCPCount > 0)
					Methods.SendSubtitle(Subtitles.MTFRespawnSCPs.Replace("{0}", regular).Replace("{1}", SCPCount.ToString()), 30);
				else
					Methods.SendSubtitle(Subtitles.MTFRespawnNOSCPs.Replace("{0}", regular), 30);
			}
		}
	}
}
