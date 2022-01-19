using System;
using System.Collections.Generic;
using System.IO;

namespace SanyaPlugin
{
	public class PlayerDataManager
	{
		public Dictionary<string, PlayerData> PlayerDataDict { get; private set; } = new Dictionary<string, PlayerData>();

		public PlayerData LoadPlayerData(string userid)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, $"{userid}.txt");
			if(!Directory.Exists(SanyaPlugin.Instance.Config.DataDirectory)) Directory.CreateDirectory(SanyaPlugin.Instance.Config.DataDirectory);
			if(!File.Exists(targetuseridpath)) return new PlayerData(DateTime.Now, userid, true, true, 0, 0, 0);
			else return ParsePlayerData(targetuseridpath);
		}

		public void SavePlayerData(PlayerData data)
		{
			string targetuseridpath = Path.Combine(SanyaPlugin.Instance.Config.DataDirectory, $"{data.userid}.txt");

			if(!Directory.Exists(SanyaPlugin.Instance.Config.DataDirectory)) Directory.CreateDirectory(SanyaPlugin.Instance.Config.DataDirectory);

			string[] textdata = new string[] {
				data.lastUpdate.ToString("yyyy-MM-ddTHH:mm:sszzzz"),
				data.userid,
				data.steamlimited.ToString(),
				data.steamvacbanned.ToString(),
				data.level.ToString(),
				data.exp.ToString(),
				data.playingcount.ToString()
			};

			File.WriteAllLines(targetuseridpath, textdata);
		}

		private PlayerData ParsePlayerData(string path)
		{
			var text = File.ReadAllLines(path);
			return new PlayerData(
				DateTime.Parse(text[0]),
				text[1],
				bool.Parse(text[2]),
				bool.Parse(text[3]),
				int.Parse(text[4]),
				int.Parse(text[5]),
				int.Parse(text[6])
				);
		}

		[Obsolete("普段は使いません", false)]
		public void ReloadParams()
		{
			foreach(var file in Directory.GetFiles(SanyaPlugin.Instance.Config.DataDirectory))
			{
				if(!file.Contains("@")) continue;
				var data = LoadPlayerData(file.Replace(".txt", string.Empty));
				data.steamlimited = true;
				data.steamvacbanned = true;
				SavePlayerData(data);
			}
		}
	}

}
