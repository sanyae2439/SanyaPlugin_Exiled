using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(AlphaWarheadController), nameof(AlphaWarheadController.CancelDetonation), new Type[] { typeof(GameObject) })]
	public static class AutoNukeReEnablePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();
			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldarg_0);

			newInst.RemoveRange(index, 3);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
