using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(CharacterClassManager), nameof(CharacterClassManager.CallCmdRegisterEscape))]
	public static class RemoveEscapeCounterPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var cuffedClassDindex = newInst.FindIndex(x => x.opcode == OpCodes.Ldsfld
				&& x.operand is FieldInfo fieldInfo
				&& fieldInfo.Name == nameof(RoundSummary.escaped_scientists)
			);
			newInst.RemoveRange(cuffedClassDindex, 4);

			var cuffedScientistindex = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldsfld
				&& x.operand is FieldInfo fieldInfo
				&& fieldInfo.Name == nameof(RoundSummary.escaped_ds)
			);
			newInst.RemoveRange(cuffedScientistindex, 4);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
