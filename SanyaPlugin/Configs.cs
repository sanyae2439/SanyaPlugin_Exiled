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
		public Dictionary<string, string> Defaultitems { get; set; } = new Dictionary<string, string>()
		{
			{ RoleType.ClassD.ToString(), ItemType.None.ToString() }
		};
		public readonly Dictionary<RoleType, List<ItemType>> DefaultitemsParsed = new();

		[Description("各ロールの初期所持弾数")]
		public Dictionary<string, Dictionary<string, ushort>> DefaultAmmos { get; set; } = new Dictionary<string, Dictionary<string, ushort>>()
		{
			{ RoleType.ClassD.ToString(), new Dictionary<string, ushort>(){ { ItemType.Ammo556x45.ToString(), (ushort)0 }  } }
		};
		public readonly Dictionary<RoleType, Dictionary<ItemType, ushort>> DefaultammosParsed = new();

		[Description("Dクラスへのロールごとの追加アイテム")]
		public Dictionary<string, string> ClassdBonusitemsForRole { get; set; } = new Dictionary<string, string>();
		public readonly Dictionary<string, List<ItemType>> ClassdBonusitemsForRoleParsed = new();

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

		[Description("プレイヤー情報にHPを表示する")]
		public bool PlayersInfoShowHp { get; set; } = false;

		[Description("プレイヤー情報のMTF関係を無効にする")]
		public bool PlayersInfoDisableFollow { get; set; } = false;

		[Description("ラウンド終了後に無敵になる")]
		public bool GodmodeAfterEndround { get; set; } = false;

		[Description("全プレイヤーのボイスチャットを無効化する")]
		public bool DisableAllChat { get; set; } = false;

		[Description("ホワイトリストに入っているプレイヤーはボイスチャット無効の対象外にする")]
		public bool DisableChatBypassWhitelist { get; set; } = false;

		[Description("ホワイトリストに入っていないミュートされたプレイヤーへのメッセージ")]
		public string MotdMessageOnDisabledChat { get; set; } = string.Empty;

		[Description("リスポーン場所をランダムにする")]
		public int RandomRespawnPosPercent { get; set; } = -1;

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

		[Description("地上のオブジェクトを編集する")]
		public bool EditObjectsOnSurface { get; set; } = false;

		[Description("アイテムを追加する")]
		public bool AddItemsOnFacility { get; set; } = false;

		[Description("Dクラス収容房の初期動作")]
		public bool ClassdPrisonInit { get; set; } = false;

		[Description("SCP-106がいない場合は囮コンテナを閉鎖する")]
		public bool Scp106ChamberLockWhenUnbreached { get; set; } = false;

		[Description("SCP-914にコインのレシピを追加する")]
		public bool Scp914AddCoinRecipes { get; set; } = false;

		[Description("拳銃にエフェクトを追加する")]
		public bool HandgunEffect { get; set; } = false;

		[Description("ヘビィアーマーのダメージ固定減衰率")]
		public float HeavyArmorDamageEfficacy { get; set; } = 1f;

		[Description("MicroHIDの威力の乗数")]
		public float MicrohidDamageMultiplier { get; set; } = 1f;

		[Description("ディスラプターの威力の乗数")]
		public float DisruptorDamageMultiplier { get; set; } = 1f;

		[Description("リボルバーの威力の乗数")]
		public float RevolverDamageMultiplier { get; set; } = 1f;

		[Description("ジャンプで消費するスタミナ量")]
		public float StaminaCostJump { get; set; } = -1f;

		[Description("MTF/CIが武装解除されると死亡し、相手チームのチケットを加算させる量")]
		public int CuffedTicketDeathToMtfCi { get; set; } = 0;

		[Description("SCP-049のシールド最大値")]
		public int Scp049MaxAhp { get; set; } = 0;

		[Description("SCP-049のシールド回復速度")]
		public float Scp049RegenRate { get; set; } = 0f;

		[Description("SCP-049のシールド回復までの時間")]
		public float Scp049TimeUntilRegen { get; set; } = 0f;

		[Description("SCP-049の治療中に受けるダメージ乗数")]
		public float Scp049TakenDamageWhenCureMultiplier { get; set; } = 1f;

		[Description("SCP-049の治療が可能な状態を延長する時間")]
		public double Scp049AddAllowrecallTime { get; set; } = 0;

		[Description("SCP-049の移動速度バフ")]
		public byte Scp049SpeedupAmount { get; set; } = 0;

		[Description("SCP-049-2の攻撃力")]
		public float Scp0492Damage { get; set; } = 40f;

		[Description("SCP-049-2の攻撃にエフェクトを追加する")]
		public bool Scp0492AttackEffect { get; set; } = false;

		[Description("SCP-049-2のスポーン時にエフェクトを追加する")]
		public bool Scp0492GiveEffectOnSpawn { get; set; } = false;

		[Description("SCP-049-2がキルするたびにHPが回復し、追加効果を得る")]
		public bool Scp0492KillStreak { get; set; } = false;

		[Description("SCP-096をリワークする")]
		public bool Scp096Rework { get; set; } = false;

		[Description("SCP-106のシールド最大値")]
		public int Scp106MaxAhp { get; set; } = 0;

		[Description("SCP-106のシールド回復速度")]
		public float Scp106RegenRate { get; set; } = 0f;

		[Description("SCP-106のシールド回復までの時間")]
		public float Scp106TimeUntilRegen { get; set; } = 0f;

		[Description("SCP-106のポータルを拡大してエフェクトを適用する")]
		public bool Scp106PortalWithSinkhole { get; set; } = false;

		[Description("SCP-106にEX-HotKeyを適用する")]
		public bool Scp106ExHotkey { get; set; } = false;

		[Description("SCP-939-XXがVC使用中の人間を視認できるように")]
		public bool Scp939CanSeeVoiceChatting { get; set; } = false;

		[Description("SCP-939-XXの攻撃で即死するようにする")]
		public bool Scp939InstaKill { get; set; } = false;

		[Description("SCP-079が部屋を移動した際にスキャンするように")]
		public bool Scp079ScanRoom { get; set; } = false;

		[Description("SCP-079にEX-Hotkeyを適用する")]
		public bool Scp079ExHotkey { get; set; } = false;

		public void ParseConfig()
		{
			try
			{
				DefaultitemsParsed.Clear();
				DefaultammosParsed.Clear();
				ClassdBonusitemsForRoleParsed.Clear();
				NoDropItemsParsed.Clear();
				AltvoicechatScpsParsed.Clear();
				TeslaDisabledTeamsParsed.Clear();

				foreach(var key in Defaultitems)
					if(Enum.TryParse(key.Key, out RoleType role))
						DefaultitemsParsed.Add(role, new List<ItemType>(key.Value.Split(',').Select((string x) => (ItemType)Enum.Parse(typeof(ItemType), x))));
					else
						Log.Error($"Defaultitems parse error: {key.Key} is not valid RoleType");

				foreach(var key in ClassdBonusitemsForRole)
					ClassdBonusitemsForRoleParsed.Add(key.Key, new List<ItemType>(key.Value.Split(',').Select((string x) => (ItemType)Enum.Parse(typeof(ItemType), x))));

				foreach(var key in DefaultAmmos)
				{
					if(Enum.TryParse(key.Key, out RoleType role))
					{
						DefaultammosParsed.Add(role, new Dictionary<ItemType, ushort>());

						foreach(var key2 in key.Value)
							if(Enum.TryParse(key2.Key, out ItemType itemType))
								DefaultammosParsed[role].Add(itemType, key2.Value);
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