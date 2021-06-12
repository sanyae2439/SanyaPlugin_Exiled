using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CustomPlayerEffects;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Decontaminating), nameof(Decontaminating.PublicUpdate))]
	public static class RemoveDecontPosCheckPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var newInst = instructions.ToList();

			var index = newInst.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fieldInfo && fieldInfo.Name == nameof(PlayerEffect.Hub)) - 1;

			newInst.RemoveRange(index, 15);

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
