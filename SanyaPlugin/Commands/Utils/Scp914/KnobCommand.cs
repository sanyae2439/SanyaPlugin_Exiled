using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils.Scp914
{
	public class KnobCommand : ICommand
	{
		public string Command { get; } = "knob";

		public string[] Aliases { get; }

		public string Description { get; } = "SCP-914のノブを変更する";

		public string RequiredPermission { get; } = "sanya.utils.914.knob";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(Exiled.API.Features.Scp914.Scp914Controller._remainingCooldown > 0f)
			{
				response = "SCP-914がクールタイム中です。";
				return false;
			}

			response = $"[{Exiled.API.Features.Scp914.Scp914Controller.Network_knobSetting}] -> ";
			Exiled.API.Features.Scp914.Scp914Controller.ServerInteract(Server.Host.ReferenceHub, 0);
			response += $"[{Exiled.API.Features.Scp914.Scp914Controller.Network_knobSetting}]";
			return true;
		}
	}
}
