using System;
using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;
using Mirror;

namespace SanyaPlugin.Commands.Utils
{
	public class LevelCommand : ICommand
	{
		public string Command { get; } = "level";

		public string[] Aliases { get; }

		public string Description { get; } = "レベル用タグを再表示できます";

		public string RequiredPermission { get; } = "sanya.utils.level";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			var player = Player.Get(sender);
			if(player == null)
			{
				response = "このコマンドはプレイヤーのみ使用できます。";
				return false;
			}

			if(SanyaPlugin.Instance.Config.DataEnabled 
				&& SanyaPlugin.Instance.Config.LevelEnabled 
				&& SanyaPlugin.Instance.Config.LevelBadgeEnabled 
				&& SanyaPlugin.Instance.PlayerDataManager.PlayerDataDict.TryGetValue(player.UserId, out PlayerData data))
				SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.GrantedLevel(player, data), Segment.FixedUpdate));

			response = $"再表示しました。";
			return true;
		}
	}
}
