using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Generator079), nameof(Generator079.LateUpdate))]
	public static class PreventGeneratorReductPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Call && x.operand is MethodBase methodBase && methodBase.Name == $"set_{nameof(Generator079.NetworkremainingPowerup)}") - 5;

			newInst.RemoveRange(index, 6);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
