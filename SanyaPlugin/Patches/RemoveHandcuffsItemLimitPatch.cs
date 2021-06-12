using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	//transpiler
	[HarmonyPatch(typeof(Handcuffs), nameof(Handcuffs.CallCmdCuffTarget))]
	public static class RemoveHandcuffsItemLimitPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldc_I4_M1) - 4;

			newInst.RemoveRange(index, 6);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
