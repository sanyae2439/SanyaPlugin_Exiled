using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using SanyaPlugin.Components;

namespace SanyaPlugin.Commands.Items
{
	public class MoveLightCommand : ICommand
	{
		public string Command { get; } = "movelight";

		public string[] Aliases { get; }

		public string Description { get; } = "ライトを発生させる";

		public string RequiredPermission { get; } = "sanya.items.movelight";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(arguments.Count == 3)
			{
				var target = Player.Get(arguments.At(0));
				if(target == null)
				{
					response = "ターゲットが見つかりません。";
					return false;
				}
				if(target.GameObject.TryGetComponent<LightMoveComponent>(out var comp))
				{
					UnityEngine.Object.Destroy(comp);
					response = $"{target.Nickname}から削除しました。";
				}
				else
				{
					var comp2 = target.GameObject.AddComponent<LightMoveComponent>();
					comp2.Intensity = float.Parse(arguments.At(1));
					comp2.Range = float.Parse(arguments.At(2));
					response = $"{target.Nickname}に設置しました。";
				}
				return true;
			}
			else
			{
				response = "引数:[player] [intensity] [range]";
				return false;
			}
		}
	}
}
