using System.Collections.Generic;
using MEC;
using UnityEngine;

namespace SanyaPlugin
{
	public class TpsWatcher
	{
		public static CoroutineHandle Coroutine { get; private set; }
		public static float CurrentTPS { get; private set; }
		public static int CurrentTPSInt { get; private set; }

		public TpsWatcher() => Coroutine = Timing.RunCoroutine(Main(), Segment.Update, "TPSChecker");
		public IEnumerator<float> Main()
		{
			while(true)
			{
				CurrentTPS = 1f / Time.deltaTime;
				CurrentTPSInt = Mathf.FloorToInt(CurrentTPS);
				yield return Timing.WaitForOneFrame;
			}
		}
	}
}
