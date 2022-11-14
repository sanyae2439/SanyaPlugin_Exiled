using HarmonyLib;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(RoundSummary), nameof(RoundSummary.OnClassChanged))]
	public static class FixSummaryCounterPatch
	{
		public static bool Prefix(ReferenceHub userHub, RoleType prevClass, RoleType newClass, bool lite, CharacterClassManager.SpawnReason spawnReason)
		{
			if(spawnReason == CharacterClassManager.SpawnReason.Escaped)
			{
				switch(prevClass)
				{
					case RoleType.ClassD:
						if(newClass == RoleType.ChaosConscript)
							RoundSummary.EscapedClassD++;
						break;
					case RoleType.Scientist:
						if(newClass == RoleType.NtfSpecialist)
							RoundSummary.EscapedScientists++;
						break;
				}
			}
			else if(spawnReason == CharacterClassManager.SpawnReason.Revived && prevClass == RoleType.Spectator && newClass == RoleType.Scp0492)
			{
				RoundSummary.ChangedIntoZombies++;
				RoundSummary.singleton.classlistStart.scps_except_zombies++;
				RoundSummary.singleton.classlistStart.zombies++;
			}

			return false;
		}
	}
}
