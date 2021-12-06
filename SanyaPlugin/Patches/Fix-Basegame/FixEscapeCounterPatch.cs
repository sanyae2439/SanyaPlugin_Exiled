using HarmonyLib;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(RoundSummary), nameof(RoundSummary.OnClassChanged))]
	public static class FixEscapeCounterPatch
	{
		public static bool Prefix(ReferenceHub userHub, RoleType prevClass, RoleType newClass, bool lite, CharacterClassManager.SpawnReason spawnReason)
		{
			if(spawnReason == CharacterClassManager.SpawnReason.Escaped)
			{
				switch(prevClass)
				{
					case RoleType.ClassD:
						RoundSummary.EscapedClassD++;
						break;
					case RoleType.Scientist:
						RoundSummary.EscapedScientists++;
						break;
				}
			}
			else if(spawnReason == CharacterClassManager.SpawnReason.Revived)
			{
				if(prevClass == RoleType.Spectator && newClass == RoleType.Scp0492)
					RoundSummary.ChangedIntoZombies++;
			}

			return false;
		}
	}
}
