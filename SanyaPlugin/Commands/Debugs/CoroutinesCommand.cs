using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using MEC;
using NorthwoodLib.Pools;

namespace SanyaPlugin.Commands.Debugs
{
	[CommandHandler(typeof(SanyaCommand))]
	public class CoroutinesCommand : ICommand
	{
		public string Command { get; } = "coroutines";

		public string[] Aliases { get; }

		public string Description { get; } = "稼働中のCoroutineの数を表示する";

		public string RequiredPermission { get; } = "sanya.debugs.coroutines";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			var stringBuilder = StringBuilderPool.Shared.Rent();

			stringBuilder.AppendLine("[稼働中のCoroutine]");
			stringBuilder.AppendLine($"FixedUpdate:{Timing.Instance.FixedUpdateCoroutines}");
			stringBuilder.AppendLine($"Update:{Timing.Instance.UpdateCoroutines}");
			stringBuilder.AppendLine($"LateUpdate:{Timing.Instance.LateUpdateCoroutines}");
			stringBuilder.AppendLine($"SlowUpdate:{Timing.Instance.SlowUpdateCoroutines}");
			stringBuilder.AppendLine($"RealtimeUpdate:{Timing.Instance.RealtimeUpdateCoroutines}");
			stringBuilder.AppendLine($"EditorUpdate:{Timing.Instance.EditorUpdateCoroutines}");
			stringBuilder.AppendLine($"EditorSlowUpdate:{Timing.Instance.EditorSlowUpdateCoroutines}");
			stringBuilder.AppendLine($"EndOfFrameUpdate:{Timing.Instance.EndOfFrameCoroutines}");
			stringBuilder.AppendLine($"ManualTimeframeCoroutines:{Timing.Instance.ManualTimeframeCoroutines}");

			response = stringBuilder.ToString();
			StringBuilderPool.Shared.Return(stringBuilder);
			return true;
		}
	}
}