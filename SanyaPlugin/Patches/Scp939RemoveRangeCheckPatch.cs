using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp939PlayerScript), nameof(Scp939PlayerScript.CallCmdShoot))]
	public static class Scp939RemoveRangeCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldc_R4) + 2;
			var nextlabel = newInst[newInst.FindIndex(x => x.opcode == OpCodes.Ret) - 1].operand;

			newInst[index - 1].opcode = OpCodes.Ble_Un_S;
			newInst[index - 1].operand = nextlabel;
			newInst.RemoveRange(index, 12);

			var popindex = newInst.FindIndex(x => x.opcode == OpCodes.Pop) + 1;
			newInst.RemoveRange(popindex, 17);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
