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

		[Description("ShootingRangeモード")]
		public bool EnabledShootingRange { get; set; } = false;

		[Description("各ロールの初期装備")]
		public Dictionary<string, string> Defaultitems { get; set; } = new Dictionary<string, string>()
		{
			{ RoleType.ClassD.ToString(), ItemType.None.ToString() }
		};
		public readonly Dictionary<RoleType, List<ItemType>> DefaultitemsParsed = new Dictionary<RoleType, List<ItemType>>();

		[Description("各ロールの初期所持弾数")]
		public Dictionary<string, Dictionary<string, ushort>> DefaultAmmos { get; set; } = new Dictionary<string, Dictionary<string, ushort>>()
		{
			{ RoleType.ClassD.ToString(), new Dictionary<string, ushort>(){ { ItemType.Ammo556x45.ToString(), (ushort)0 }  } }
		};
		public readonly Dictionary<RoleType, Dictionary<ItemType, ushort>> DefaultammosParsed = new Dictionary<RoleType, Dictionary<ItemType, ushort>>();

		[Description("Dクラスへのロールごとの追加アイテム")]
		public Dictionary<string, string> ClassdBonusitemsForRole { get; set; } = new Dictionary<string, string>();
		public readonly Dictionary<string, List<ItemType>> ClassdBonusitemsForRoleParsed = new Dictionary<string, List<ItemType>>();

		[Description("Steamの制限付きユーザーをキックする")]
		public bool KickSteamLimited { get; set; } = false;

		[Description("SteamのVACBannedユーザーをキックする")]
		public bool KickSteamVacBanned { get; set; } = false;

		[Description("VPN検知に使用するIPHubのAPIキー")]
		public string KickVpnApikey { get; set; } = string.Empty;

		[Description("サーバー参加者に表示するブロードキャスト")]
		public string MotdMessage { get; set; } = string.Empty;

		[Description("Sinkholeの修正を有効にする")]
		public bool FixSinkhole { get; set; } = false;

		[Description("ExHudの有効化")]
		public bool ExHudEnabled { get; set; } = false;

		[Description("CASSIE放送に字幕を表示する")]
		public bool CassieSubtitle { get; set; } = false;

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
		public readonly List<RoleType> AltvoicechatScpsParsed = new List<RoleType>();

		[Description("AlphaWarheadがカウントダウン開始できるようになるまでの時間")]
		public int AlphaWarheadNeedElapsedSeconds { get; set; } = -1;

		[Description("AlphaWarheadを停止できないようにする")]
		public bool AlphaWarheadLockAlways { get; set; } = false;

		[Description("AlphaWarhead起爆後のリスポーン時間")]
		public int TimeToRespawnAfterDetonated { get; set; } = -1;

		[Description("キルヒットマークの表示")]
		public bool HitmarkKilled { get; set; } = false;

		[Description("発電機の使用を細かく修正する")]
		public bool GeneratorFix { get; set; } = false;

		[Description("テスラで死亡した際の死体やアイテムを削除する")]
		public bool TeslaDeleteObjects { get; set; } = false;

		[Description("特定のレベルのカードを手に持っているときはテスラが反応しなくなる")]
		public string TeslaDisabledPermission { get; set; } = "None";

		[Description("地上にドアを追加する")]
		public bool AddDoorsOnSurface { get; set; } = false;

		[Description("地上のオブジェクトを編集する")]
		public bool EditObjectsOnSurface { get; set; } = false;

		[Description("地上の明るさ")]
		public float LightIntensitySurface { get; set; } = 1f;

		[Description("SCP-914に入ると悪影響を受ける")]
		public bool Scp914Debuff { get; set; } = false;

		[Description("ジャンプで消費するスタミナ量")]
		public float StaminaCostJump { get; set; } = -1f;

		[Description("MTF/CIが武装解除されると死亡し、相手チームのチケットを加算させる量")]
		public int CuffedTicketDeathToMtfCi { get; set; } = 0;

		[Description("武装解除時の被ダメージ乗数")]
		public float CuffedDamageMultiplier { get; set; } = 1f;

		[Description("SCPの被ダメージ乗数")]
		public Dictionary<string, float> ScpTakenDamageMultiplier { get; set; } = new Dictionary<string, float>()
		{
			{ nameof(RoleType.Scp049), 1f },
			{ nameof(RoleType.Scp0492), 1f },
			{ nameof(RoleType.Scp096), 1f },
			{ nameof(RoleType.Scp106), 1f },
			{ nameof(RoleType.Scp173), 1f },
			{ nameof(RoleType.Scp93953), 1f },
			{ nameof(RoleType.Scp93989), 1f },
		};
		public readonly Dictionary<RoleType, float> ScpTakenDamageMultiplierParsed = new Dictionary<RoleType, float>();

		[Description("SCP-049のシールド最大値")]
		public int Scp049MaxAhp { get; set; } = 0;

		[Description("SCP-049のシールド回復速度")]
		public float Scp049RegenRate { get; set; } = 0f;

		[Description("SCP-049のシールド回復までの時間")]
		public float Scp049TimeUntilRegen { get; set; } = 0f;

		[Description("SCP-049-2の攻撃力")]
		public float Scp0492Damage { get; set; } = 40f;

		[Description("SCP-049-2の攻撃にエフェクトを追加する")]
		public bool Scp0492AttackEffect { get; set; } = false;

		[Description("SCP-049-2のスポーン時にエフェクトを追加する")]
		public bool Scp0492GiveEffectOnSpawn { get; set; } = false;

		[Description("SCP-049-2がキルするたびにHPが回復し、追加効果を得る")]
		public bool Scp0492KillStreak { get; set; } = false;

		[Description("SCP-096に触れると発狂するようになる距離")]
		public float Scp096TouchEnrageDistance { get; set; } = -1f;

		[Description("SCP-096の発狂時のダメージ乗数")]
		public float Scp096EnragingDamageMultiplier { get; set; } = 1f;

		[Description("SCP-106のシールド最大値")]
		public int Scp106MaxAhp { get; set; } = 0;

		[Description("SCP-106のシールド回復速度")]
		public float Scp106RegenRate { get; set; } = 0f;

		[Description("SCP-106のシールド回復までの時間")]
		public float Scp106TimeUntilRegen { get; set; } = 0f;

		[Description("SCP-106のポータルを拡大してエフェクトを適用する")]
		public bool Scp106PortalWithSinkhole { get; set; } = false;

		[Description("SCP-939-XXがVC使用中の人間を視認できるように")]
		public bool Scp939CanSeeVoiceChatting { get; set; } = false;

		[Description("SCP-939-XXの攻撃で即死するようにして移動速度を低下させる")]
		public bool Scp939InstaKill { get; set; } = false;

		[Description("SCP-079が部屋を移動した際にスキャンするように")]
		public bool Scp079ScanRoom { get; set; } = false;

		[Description("SCP-079がゲートを操作するのに必要なTier")]
		public int Scp079NeedInteractGateTier { get; set; } = -1;

		[Description("SCP-079の消費コスト")]
		public Dictionary<string, float> Scp079ManaCost { get; set; } = new Dictionary<string, float>()
		{
			{"Camera Switch",                   1f },
			{"Door Lock",                       4f },
			{"Door Lock Start",                 5f },
			{"Door Lock Minimum",              10f },
			{"Door Interaction DEFAULT",        5f },
			{"Door Interaction CONT_LVL_1",    50f },
			{"Door Interaction CONT_LVL_2",    40f },
			{"Door Interaction CONT_LVL_3",   110f },
			{"Door Interaction ARMORY_LVL_1",  50f },
			{"Door Interaction ARMORY_LVL_2",  60f },
			{"Door Interaction ARMORY_LVL_3",  70f },
			{"Door Interaction EXIT_ACC",      60f },
			{"Door Interaction INCOM_ACC",     30f },
			{"Door Interaction CHCKPOINT_ACC", 10f },
			{"Room Lockdown",                  60f },
			{"Tesla Gate Burst",               50f },
			{"Elevator Teleport",              30f },
			{"Elevator Use",                   10f },
			{"Speaker Start",                  10f },
			{"Speaker Update",                0.8f }
		};

		public void ParseConfig()
		{
			try
			{
				DefaultitemsParsed.Clear();
				DefaultammosParsed.Clear();
				ClassdBonusitemsForRoleParsed.Clear();
				AltvoicechatScpsParsed.Clear();
				ScpTakenDamageMultiplierParsed.Clear();

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

				foreach(var item in AltvoicechatScps)
					if(Enum.TryParse(item, out RoleType role))
						AltvoicechatScpsParsed.Add(role);
					else
						Log.Error($"AltvoicechatScps parse error: {item} is not valid RoleType");

				foreach(var key in ScpTakenDamageMultiplier)
					if(Enum.TryParse(key.Key, out RoleType role))
						ScpTakenDamageMultiplierParsed.Add(role, key.Value);
					else
						Log.Error($"ScpTakenDamageMultiplier parse error: {key.Key} is not valid RoleType");
			}
			catch(Exception ex)
			{
				Log.Error($"[ParseConfig] Error : {ex}");
			}
		}
	}
}