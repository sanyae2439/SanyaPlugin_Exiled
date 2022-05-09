using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using MEC;

namespace SanyaPlugin.Commands.Utils
{
	public class RainbowFacilityCommand : ICommand
	{
		public string Command { get; } = "rainbow";

		public string[] Aliases { get; }

		public string Description { get; } = "施設が虹色になります";

		public string RequiredPermission { get; } = "sanya.utils.rainbow";

		public static bool isActive = false;
		public CoroutineHandle coroutine;

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(!isActive)
			{
				isActive = true;
				coroutine = Timing.RunCoroutine(Coroutines.RainbowFacility(), Segment.FixedUpdate);	
			}
			else
			{
				isActive = false;
			}
			response = $"{isActive}にしました。";
			return true;
		}
	}
}
