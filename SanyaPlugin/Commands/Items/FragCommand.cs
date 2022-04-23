using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Items
{
	internal class FragCommand : ICommand
	{
		public string Command { get; } = "frag";

		public string[] Aliases { get; }

		public string Description { get; } = "フラググレネードを設置する";

		public string RequiredPermission { get; } = "sanya.items.frag";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(arguments.Count == 1)
			{
				var target = Player.Get(arguments.At(0));
				if(target == null)
				{
					response = "ターゲットが見つかりません。";
					return false;
				}
				Methods.SpawnGrenade(target.Position, ItemType.GrenadeHE, -1f, target.ReferenceHub);
				response = $"{target.Nickname}に設置しました。";
				return true;
			}
			else
			{
				response = "引数:[players or *]";
				return false;
			}
		}
	}
}
