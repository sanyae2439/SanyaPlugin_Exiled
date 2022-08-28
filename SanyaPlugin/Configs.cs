using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace SanyaPlugin
{
	public sealed class Configs : IConfig
	{
		public Configs() => DataDirectory = Path.Combine(Paths.Configs, "SanyaPlugin");

		[Description("さにゃぷらぐいんの有効化")]
		public bool IsEnabled { get; set; } = true;

		[Description("デバッグメッセージの有効化")]
		public bool IsDebugged { get; set; } = false;

		[Description("プレイヤーデータの有効化")]
		public bool DataEnabled { get; set; } = false;

		[Description("プレイヤーデータの場所")]
		public string DataDirectory { get; private set; } = string.Empty;

		[Description("プレイヤーレベルの有効化")]
		public bool LevelEnabled { get; set; } = false;

		[Description("プレイヤーレベルをバッジに表示するか")]
		public bool LevelBadgeEnabled { get; set; } = false;

		[Description("キル時に手に入るレベル経験値")]
		public int LevelExpKill { get; set; } = 3;

		[Description("デス時に手に入るレベル経験値")]
		public int LevelExpDeath { get; set; } = 1;

		[Description("勝利時に手に入るレベル経験値")]
		public int LevelExpWin { get; set; } = 10;

		[Description("敗北時に手に入るレベル経験値")]
		public int LevelExpLose { get; set; } = 1;

		[Description("サーバー情報送信先IPアドレス")]
		public string InfosenderIp { get; set; } = "none";

		[Description("サーバー情報送信先UDPポート")]
		public int InfosenderPort { get; set; } = -1;

		[Description("イベントモードのウェイト設定")]
		public List<int> EventModeWeight { get; set; } = new List<int>() { 0, 0 };

		[Description("各ロールの初期装備")]
		public Dictionary<string, string> DefaultItems { get; set; } = new Dictionary<string, string>()
		{
			{ RoleType.ClassD.ToString(), ItemType.None.ToString() }
		};
		public readonly Dictionary<RoleType, List<ItemType>> DefaultItemsParsed = new();

		[Description("各ロールの初期所持弾数")]
		public Dictionary<string, Dictionary<string, ushort>> DefaultAmmos { get; set; } = new Dictionary<string, Dictionary<string, ushort>>()
		{
			{ RoleType.ClassD.ToString(), new Dictionary<string, ushort>(){ { ItemType.Ammo556x45.ToString(), (ushort)0 }  } }
		};
		public readonly Dictionary<RoleType, Dictionary<ItemType, ushort>> DefaultAmmosParsed = new();

		[Description("落とさないようにするアイテム")]
		public List<string> NoDropItems { get; set; } = new List<string>();
		public readonly List<ItemType> NoDropItemsParsed = new();

		[Description("Steamの制限付きユーザーをキックする")]
		public bool KickSteamLimited { get; set; } = false;

		[Description("SteamのVACBannedユーザーをキックする")]
		public bool KickSteamVacBanned { get; set; } = false;

		[Description("VPN検知に使用するIPHubのAPIキー")]
		public string KickVpnApikey { get; set; } = string.Empty;

		[Description("サーバー参加者に表示するブロードキャスト")]
		public string MotdMessage { get; set; } = string.Empty;

		[Description("ExHudの有効化")]
		public bool ExHudEnabled { get; set; } = false;

		[Description("放送室のモニターの表示を拡張する")]
		public bool IntercomInformation { get; set; } = false;

		[Description("全プレイヤーのボイスチャットを無効化する")]
		public bool DisableAllChat { get; set; } = false;

		[Description("ホワイトリストに入っているプレイヤーはボイスチャット無効の対象外にする")]
		public bool DisableChatBypassWhitelist { get; set; } = false;

		[Description("ホワイトリストに入っていないミュートされたプレイヤーへのメッセージ")]
		public string MotdMessageOnDisabledChat { get; set; } = string.Empty;

		[Description("Vキーチャットが可能なSCP（SCP-939以外）")]
		public List<string> AltvoicechatScps { get; set; } = new List<string>();
		public readonly List<RoleType> AltvoicechatScpsParsed = new();

		[Description("SCPが切断した場合にSCPを再度スポーンする")]
		public bool SpawnScpsWhenDisconnect { get; set; } = false;

		[Description("キルヒットマークの表示")]
		public bool HitmarkKilled { get; set; } = false;

		[Description("ポケットディメンションの死体やアイテムを発生させない")]
		public bool PocketdimensionClean { get; set; } = false;

		[Description("テスラで死亡した際の死体やアイテムを削除する")]
		public bool TeslaDeleteObjects { get; set; } = false;

		[Description("指定したチームがテスラに反応しなくなる")]
		public List<string> TeslaDisabledTeams { get; set; } = new List<string>();
		public readonly List<Team> TeslaDisabledTeamsParsed = new();

		[Description("SCP-049をリワークする")]
		public bool Scp049Rework { get; set; } = false;

		[Description("SCP-096をリワークする")]
		public bool Scp096Rework { get; set; } = false;

		public void ParseConfig()
		{
			try
			{
				DefaultItemsParsed.Clear();
				DefaultAmmosParsed.Clear();
				NoDropItemsParsed.Clear();
				AltvoicechatScpsParsed.Clear();
				TeslaDisabledTeamsParsed.Clear();

				foreach(var key in DefaultItems)
					if(Enum.TryParse(key.Key, out RoleType role))
						DefaultItemsParsed.Add(role, new List<ItemType>(key.Value.Split(',').Select((string x) => (ItemType)Enum.Parse(typeof(ItemType), x))));
					else
						Log.Error($"Defaultitems parse error: {key.Key} is not valid RoleType");

				foreach(var key in DefaultAmmos)
				{
					if(Enum.TryParse(key.Key, out RoleType role))
					{
						DefaultAmmosParsed.Add(role, new Dictionary<ItemType, ushort>());

						foreach(var key2 in key.Value)
							if(Enum.TryParse(key2.Key, out ItemType itemType))
								DefaultAmmosParsed[role].Add(itemType, key2.Value);
							else
								Log.Error($"DefaultAmmos parse error: {key2.Key} is not valid ItemType");
					}
					else
						Log.Error($"DefaultAmmos parse error: {key.Key} is not valid RoleType");
				}

				foreach(var item in NoDropItems)
					if(Enum.TryParse(item, out ItemType itemtype))
						NoDropItemsParsed.Add(itemtype);
				    else
						Log.Error($"NoDropItems parse error: {item} is not valid ItemType");

				foreach(var item in AltvoicechatScps)
					if(Enum.TryParse(item, out RoleType role))
						AltvoicechatScpsParsed.Add(role);
					else
						Log.Error($"AltvoicechatScps parse error: {item} is not valid RoleType");

				foreach(var item in TeslaDisabledTeams)
					if(Enum.TryParse(item, out Team team))
						TeslaDisabledTeamsParsed.Add(team);
					else
						Log.Error($"TeslaDisabledTeams parse error: {item} is not valid Team");
			}
			catch(Exception ex)
			{
				Log.Error($"[ParseConfig] Error : {ex}");
			}
		}
	}
}