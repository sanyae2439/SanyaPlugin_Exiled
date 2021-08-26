using System;
using System.Collections.Generic;
using HarmonyLib;
using MEC;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Timing), nameof(Timing.RunCoroutine), new Type[] { typeof(IEnumerator<float>), typeof(Segment) })]
	public static class OverrideSegmentPatch
	{
		public static void Prefix(ref Segment segment) => segment = Segment.FixedUpdate;
	}
}