using HarmonyLib;
using Scp2536;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp2536Controller), nameof(Scp2536Controller.ServerFindTarget))]
	public static  class Force2536Patch
	{
		public static ReferenceHub forceTarget = null;
		public static void Postfix(ref ReferenceHub __result)
		{
			if(forceTarget != null)
				__result = forceTarget;
		}
	}
}
