using HarmonyLib;
using Hints;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(HintDisplay), nameof(HintDisplay.Show))]
	public static class OverrideHintPatch
	{
		public static bool Prefix(Hint hint)
		{
			if(!SanyaPlugin.Instance.Config.ExHudEnabled) return true;

			if(hint.GetType() == typeof(TranslationHint))
				return false;

			if(hint._effects != null && hint._effects.Length > 0)
				return false;

			return true;
		}
	}
}
