using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Update))]
	public static class StopRagdollDecayedPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldarg_0);

			newInst.RemoveRange(index, 6);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
