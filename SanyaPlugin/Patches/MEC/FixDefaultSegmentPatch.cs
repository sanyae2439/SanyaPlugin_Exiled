using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MEC;

namespace SanyaPlugin.Patches.MEC
{
	[HarmonyPatch(typeof(Timing), nameof(Timing.RunCoroutine), new Type[] { typeof(IEnumerator<float>) })]
	public static class FixDefaultSegmentPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			newInst.Find(x => x.opcode == OpCodes.Ldc_I4_0).opcode = OpCodes.Ldc_I4_1;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}