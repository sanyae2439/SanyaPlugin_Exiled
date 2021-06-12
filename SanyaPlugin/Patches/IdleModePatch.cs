using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.RemovePlayer))]
	public static class IdleModePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();
			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Brtrue_S) + 1;

			newInst.InsertRange(index, new[]
			{
				new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(RoundSummary), nameof(RoundSummary.singleton))),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RoundSummary), nameof(RoundSummary._roundEnded))),
				new CodeInstruction(OpCodes.Brtrue_S, newInst[newInst.Count - 1].labels[0])
			});

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
