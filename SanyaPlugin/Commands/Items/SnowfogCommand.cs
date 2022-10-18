using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using SanyaPlugin.Components;

namespace SanyaPlugin.Commands.Items
{
	public class SnowfogCommand : ICommand
	{
		public string Command { get; } = "snowfog";

		public string[] Aliases { get; }

		public string Description { get; } = "Scp244の煙を対象に設置する";

		public string RequiredPermission { get; } = "sanya.items.snowfog";

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
				if(target.GameObject.TryGetComponent<Scp244MoveComponent>(out var comp))
				{
					UnityEngine.Object.Destroy(comp);
					response = $"{target.Nickname}から削除しました。";
				}
				else
				{
					target.GameObject.AddComponent<Scp244MoveComponent>();
					response = $"{target.Nickname}に設置しました。";
				}
				return true;
			}
			else
			{
				response = "引数:[player]";
				return false;
			}
		}
	}
}
