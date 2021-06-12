using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.VisionInformation), nameof(PlayableScps.VisionInformation.GetVisionInformation))]
	public static class Scp096TouchRagePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			if(SanyaPlugin.Instance.Config.Scp096TouchEnrageDistance < 0f)
			{
				foreach(var vanillaInst in instructions.ToList())
					yield return vanillaInst;
				yield break;
			}

			var newInst = instructions.ToList();

			var forceLoSindex = newInst.FindIndex(x => x.opcode == OpCodes.And);
			newInst.RemoveAt(forceLoSindex);
			newInst.RemoveAt(forceLoSindex - 2);

			var fixLoSindex = newInst.FindIndex(x => x.opcode == OpCodes.Ceq) + 2;
			var fixLoSlabel = newInst[fixLoSindex + 2].labels[0];
			newInst.InsertRange(fixLoSindex, new[] {
				new CodeInstruction(OpCodes.Ldloc_2),
				new CodeInstruction(OpCodes.Brfalse_S, fixLoSlabel)
			});

			var index = newInst.FindLastIndex(x => x.opcode == OpCodes.Ceq) + 2;
			var label = newInst[index].labels[0];
			var label2 = newInst[index].labels[1];
			newInst[index].labels.RemoveAt(1);


			newInst.InsertRange(index, new[]{
				new CodeInstruction(OpCodes.Ldloca_S, 6),
				new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(UnityEngine.Vector3), nameof(UnityEngine.Vector3.magnitude))),
				new CodeInstruction(OpCodes.Stloc_S, 5),

				new CodeInstruction(OpCodes.Ldloc_S, 7),
				new CodeInstruction(OpCodes.Brfalse_S, label),
				new CodeInstruction(OpCodes.Ldloc_S, 5),
				new CodeInstruction(OpCodes.Ldc_R4, 1.5f),
				new CodeInstruction(OpCodes.Bge_Un_S, label),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Stloc_2),
				new CodeInstruction(OpCodes.Ldc_R4, 0.1f),
				new CodeInstruction(OpCodes.Stloc_3),
			});
			newInst[index].labels.Add(label2);

			var labelindex = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldnull) - 2;
			var labelindex2 = newInst.FindLastIndex(x => x.opcode == OpCodes.Ldarg_S) - 1;
			newInst[labelindex].operand = label2;
			newInst[labelindex2].operand = label2;

			for(int i = 0; i < newInst.Count; i++)
				yield return newInst[i];
		}
	}
}
