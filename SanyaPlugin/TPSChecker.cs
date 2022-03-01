using System.Collections.Generic;
using MEC;
using UnityEngine;

namespace SanyaPlugin
{
	public class TPSChecker
	{
		public static CoroutineHandle Coroutine { get; private set; }
		public static float CurrentTPS { get; private set; }
		public static int CurrentTPSInt { get; private set; }

		public TPSChecker() => Coroutine = Timing.RunCoroutine(Main(), Segment.Update, "TPSChecker");
		public IEnumerator<float> Main()
		{
			while(true)
			{
				CurrentTPS = 1f / Time.deltaTime;
				CurrentTPSInt = Mathf.CeilToInt(CurrentTPS);
				yield return Timing.WaitForOneFrame;
			}
		}
	}
}
