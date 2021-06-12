using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp049_2PlayerScript), nameof(Scp049_2PlayerScript.CallCmdHurtPlayer))]
	public static class Scp0492RemoveRangeCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fieldInfo && fieldInfo.Name == nameof(Scp049_2PlayerScript._hub)) - 1;

			newInst.RemoveRange(index, 13);

			var label = generator.DefineLabel();
			newInst[index].labels.Add(label);
			newInst[index - 2].operand = label;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
