﻿using System;
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
		public List<int> EventModeWeight { get; set; } = new List<int>() { 0, 0, 0, 0 };

		[Description("各ロールの初期装備")]
		public Dictionary<string, string> Defaultitems { get; set; } = new Dictionary<string, string>()
		{
			{ RoleType.ClassD.ToString(), ItemType.None.ToString() }
		};
		public readonly Dictionary<RoleType, List<ItemType>> DefaultitemsParsed = new Dictionary<RoleType, List<ItemType>>();

		[Description("テスラで死亡した際の死体やアイテムを削除する")]
		public bool TeslaDeleteObjects { get; set; } = false;

		[Description("追加でアイテムを設置する")]
		public bool SpawnAddItems { get; set; } = false;

		[Description("Steamの制限付きユーザーをキックする")]
		public bool KickSteamLimited { get; set; } = false;

		[Description("SteamのVACBannedユーザーをキックする")]
		public bool KickSteamVacBanned { get; set; } = false;

		[Description("VPN検知に使用するIPHubのAPIキー")]
		public string KickVpnApikey { get; set; } = string.Empty;

		[Description("指定されたPingを超えたプレイヤーはキックされる")]
		public int PingLimit { get; set; } = -1;

		[Description("サーバー参加者に表示するブロードキャスト")]
		public string MotdMessage { get; set; } = string.Empty;

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

		[Description("発電機のロックが解除された状態になる")]
		public bool UnlockedGenerators { get; set; } = false;

		[Description("発電機のアンロックと同時にドアを開ける")]
		public bool GeneratorUnlockOpen { get; set; } = false;

		[Description("発電機の終了時にドアを閉じてロックする")]
		public bool GeneratorFinishLock { get; set; } = false;

		[Description("核の初期カウントダウン時間(30-120)")]
		public int WarheadInitCountdown { get; set; } = -1;

		[Description("SCPが0になった際に自動核を起爆するまでの時間")]
		public int WarheadAutoWhenNoScps { get; set; } = -1;

		[Description("核起爆後に地上エリアの空爆が開始するまでの秒数")]
		public int OutsidezoneTerminationTimeAfterNuke { get; set; } = -1;

		[Description("ラウンド終了後に無敵になる")]
		public bool GodmodeAfterEndround { get; set; } = false;

		[Description("全プレイヤーのボイスチャットを無効化する")]
		public bool DisableAllChat { get; set; } = false;

		[Description("観戦者のボイスチャットを無効化する")]
		public bool DisableSpectatorChat { get; set; } = false;

		[Description("ホワイトリストに入っているプレイヤーはボイスチャット無効の対象外にする")]
		public bool DisableChatBypassWhitelist { get; set; } = false;

		[Description("ホワイトリストに入っていないミュートされたプレイヤーへのメッセージ")]
		public string MotdMessageOnDisabledChat { get; set; } = string.Empty;

		[Description("リスポーン場所をランダムにする")]
		public int RandomRespawnPosPercent { get; set; } = -1;

		[Description("Vキーチャットが可能なSCP（SCP-939以外）")]
		public List<string> AltvoicechatScps { get; set; } = new List<string>();
		public readonly List<RoleType> AltvoicechatScpsParsed = new List<RoleType>();

		[Description("核起爆後の増援を停止する")]
		public bool StopRespawnAfterDetonated { get; set; } = false;

		[Description("地上のドアなどを改装する")]
		public bool EditMapOnSurface { get; set; } = false;

		[Description("ランダムでキーカードが不要なドアが開いているようになる確率")]
		public float RandomOpenNotPermissionDoors { get; set; } = -1f;

		[Description("フラッシュバンが壁にぶつかった後1秒で起爆する")]
		public bool FlashbangFuseWithCollision { get; set; } = false;

		[Description("特定のレベルのカードを手に持っているときはテスラが反応しなくなる")]
		public string TeslaDisabledPermission { get; set; } = "None";

		[Description("グレネードが命中するとヒットマークが出るように")]
		public bool HitmarkGrenade { get; set; } = false;

		[Description("キルすると大きいヒットマークが出るように")]
		public bool HitmarkKilled { get; set; } = false;

		[Description("ジャンプで消費するスタミナ量")]
		public float StaminaCostJump { get; set; } = -1f;

		[Description("ジャンプ中にスニークキーで蹴りを出せるように")]
		public bool JumpingKickAttack { get; set; } = false;

		[Description("武装解除時の被ダメージ乗数")]
		public float CuffedDamageMultiplier { get; set; } = 1f;

		[Description("SCP-914に入ると悪影響を受ける")]
		public bool Scp914Debuff { get; set; } = false;

		[Description("SCP-018のダメージ乗数")]
		public float Scp018DamageMultiplier { get; set; } = 1f;

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

		[Description("SCP-049の治療成功時追加AHP量")]
		public int Scp049CureAhpAmount { get; set; } = 0;

		[Description("SCP-049が殺害時に死体をスタックできるように")]
		public bool Scp049StackBody { get; set; } = false;

		[Description("SCP-049-2の攻撃にエフェクトを追加する")]
		public bool Scp0492AttackEffect { get; set; } = false;

		[Description("SCP-049-2がHPの量に応じて加速する")]
		public bool Scp0492SpeedupByHealthAmount { get; set; } = false;

		[Description("SCP-096に触れると発狂するようになる距離")]
		public float Scp096TouchEnrageDistance { get; set; } = -1f;

		[Description("SCP-096の発狂時のダメージ乗数")]
		public float Scp096EnragingDamageMultiplier { get; set; } = 1f;

		[Description("SCP-106のグレネードの被ダメージ乗数")]
		public float Scp106GrenadeMultiplier { get; set; } = 1f;

		[Description("SCP-106のポケットディメンションでのキル時回復量")]
		public int Scp106RecoveryAmount { get; set; } = 0;

		[Description("SCP-106のポケットディメンション転送時に増加するAHPの量")]
		public int Scp106SendPocketAhpAmount { get; set; } = 0;

		[Description("SCP-106のポケットディメンションで死亡時に回復するAHPの自然回復増加量")]
		public float Scp106SendPocketAhpDecayAmount { get; set; } = 0;

		[Description("SCP-106のExモードで収容室に帰還できるように")]
		public bool Scp106Exmode { get; set; } = false;

		[Description("SCP-106のポータルを拡大してエフェクトを適用する")]
		public bool Scp106PortalWithSinkhole { get; set; } = false;

		[Description("SCP-173の被視認者に応じて増加するAHPの一人当たりの量")]
		public int Scp173SeeingByHumansAhpAmount { get; set; } = 0;

		[Description("SCP-173のまばたきのの最小間隔")]
		public float Scp173MinBlinktime { get; set; } = 2.5f;

		[Description("SCP-173のまばたきのの最大間隔")]
		public float Scp173MaxBlinktime { get; set; } = 3.5f;

		[Description("SCP-939-XXのサイズ")]
		public float Scp939ScaleMultiplier { get; set; } = 1f;

		[Description("SCP-939-XXがHPの量で加速する")]
		public bool Scp939SpeedupByHealthAmount { get; set; } = false;

		[Description("SCP-939-XXが人間の視認数に応じてAHPを持つ際の一人当たりの量")]
		public int Scp939SeeingAhpAmount { get; set; } = 0;

		[Description("SCP-939-XXがVC使用中の人間を視認できるように")]
		public bool Scp939CanSeeVoiceChatting { get; set; } = false;

		[Description("SCP-079がゲートと914操作に必要なTier")]
		public int Scp079NeedInteractTierGateand914 { get; set; } = -1;

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

		[Description("SCP-079のExモードを有効化")]
		public bool Scp079ExtendEnabled { get; set; } = false;

		[Description("SCP-079のExモードでのスポットの必要レベル")]
		public int Scp079ExtendLevelSpot { get; set; } = 1;

		[Description("SCP-079のExモードでのSCPの位置へカメラ移動の必要レベル")]
		public int Scp079ExtendLevelFindscp { get; set; } = 2;

		[Description("SCP-079のExモードでのSCPの位置へカメラ移動のコスト")]
		public float Scp079ExtendCostFindscp { get; set; } = 10f;

		[Description("SCP-079のExモードでの核の操作の必要レベル")]
		public int Scp079ExtendLevelNuke { get; set; } = 3;

		[Description("SCP-079のExモードでの核の操作のコスト")]
		public float Scp079ExtendCostNuke { get; set; } = 50f;

		[Description("SCP-079のExモードでの爆発物起爆の必要レベル")]
		public int Scp079ExtendLevelBomb { get; set; } = 4;

		[Description("SCP-079のExモードでの爆発物起爆の必要コスト")]
		public float Scp079ExtendCostBomb { get; set; } = 50f;

		[Description("SCP-079のExモードでの地上ターゲット起爆の必要レベル")]
		public int Scp079ExtendLevelTargetBomb { get; set; } = 5;

		[Description("SCP-079のExモードでの地上ターゲット起爆の必要コスト")]
		public float Scp079ExtendCostTargetBomb { get; set; } = 75f;

		public void ParseConfig()
		{
			try
			{
				DefaultitemsParsed.Clear();
				AltvoicechatScpsParsed.Clear();
				ScpTakenDamageMultiplierParsed.Clear();

				foreach(var key in Defaultitems)
					if(Enum.TryParse(key.Key, out RoleType role))
						DefaultitemsParsed.Add(role, new List<ItemType>(key.Value.Split(',').Select((string x) => (ItemType)Enum.Parse(typeof(ItemType), x))));

				foreach(var item in AltvoicechatScps)
					if(Enum.TryParse(item, out RoleType role))
						AltvoicechatScpsParsed.Add(role);

				foreach(var key in ScpTakenDamageMultiplier)
					if(Enum.TryParse(key.Key, out RoleType role))
						ScpTakenDamageMultiplierParsed.Add(role, key.Value);
			}
			catch(Exception ex)
			{
				Log.Error($"[ParseConfig] Error : {ex}");
			}
		}
	}
}