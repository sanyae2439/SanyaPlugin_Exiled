using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp079PlayerScript), nameof(Scp079PlayerScript.Start))]
	public static class Scp079ManaCostPatch
	{
		public static void Postfix(Scp079PlayerScript __instance)
		{
			foreach(Scp079PlayerScript.Ability079 ability in __instance.abilities)
				if(SanyaPlugin.Instance.Config.Scp079ManaCost.TryGetValue(ability.label, out var value))
					ability.mana = value;
		}
	}

}
