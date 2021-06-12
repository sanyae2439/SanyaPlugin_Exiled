using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using CustomPlayerEffects;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp207), nameof(Scp207.PublicUpdate))]
	public static class Scp207PreventDamageForScpPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ret);
			var brindex = newInst.FindIndex(x => x.opcode == OpCodes.Brtrue_S);
			var exitlabel = newInst[index + 1].labels[0];
			var retlabel = newInst[index].labels[0];

			newInst[brindex].opcode = OpCodes.Brfalse_S;
			newInst[brindex].operand = retlabel;

			newInst.InsertRange(index, new[]{
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerEffect),nameof(PlayerEffect.Hub))),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ReferenceHub),nameof(ReferenceHub.characterClassManager))),
				new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(CharacterClassManager),nameof(CharacterClassManager.IsAnyScp))),
				new CodeInstruction(OpCodes.Brfalse_S, exitlabel)
			});

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
