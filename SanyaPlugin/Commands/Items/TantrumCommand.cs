using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Mirror;
using PlayableScps.ScriptableObjects;
using UnityEngine;

namespace SanyaPlugin.Commands.Items
{
	public class TantrumCommand : ICommand
	{
		public string Command { get; } = "tantrum";

		public string[] Aliases { get; }

		public string Description { get; } = "タントラムを設置する";

		public string RequiredPermission { get; } = "sanya.items.tantrum";

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
				GameObject gameObject = UnityEngine.Object.Instantiate(ScpScriptableObjects.Instance.Scp173Data.TantrumPrefab);
				gameObject.transform.position = target.ReferenceHub.playerMovementSync.RealModelPosition;
				NetworkServer.Spawn(gameObject);
				response = $"{target.Nickname}に設置しました。";
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
