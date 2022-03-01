using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils
{
	public class TpPosCommand : ICommand
	{
		public string Command { get; } = "tppos";

		public string[] Aliases { get; } = new string[] { "tpp" };

		public string Description { get; } = "指定した座標へワープする";

		public string RequiredPermission { get; } = "sanya.utils.tppos";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(arguments.Count == 4)
			{
				var target = Player.Get(arguments.At(0));
				if(target == null)
				{
					response = "ターゲットが見つかりません。";
					return false;
				}
				target.Position = new UnityEngine.Vector3(float.Parse(arguments.At(1)), float.Parse(arguments.At(2)), float.Parse(arguments.At(3)));
				response = $"{target.Nickname} -> {target.Position}";
				return true;
			}
			else
			{
				response = "引数:[player] [x] [y] [z]";
				return false;
			}
		}
	}
}
