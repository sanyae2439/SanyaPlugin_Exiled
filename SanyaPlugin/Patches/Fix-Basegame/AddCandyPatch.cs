using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using InventorySystem.Items.Usables.Scp330;
using NorthwoodLib.Pools;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	//[HarmonyPatch(typeof(CandyPink), nameof(CandyPink.SpawnChanceWeight), MethodType.Getter)]
	public static class AddCandyPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			List<CodeInstruction> newInstructions = ListPool<CodeInstruction>.Shared.Rent(instructions);

			newInstructions[newInstructions.FindIndex(x => x.opcode == OpCodes.Ldc_R4)].operand = 1f;

			for(int z = 0; z < newInstructions.Count; z++)
				yield return newInstructions[z];

			ListPool<CodeInstruction>.Shared.Return(newInstructions);
		}
	}
}
