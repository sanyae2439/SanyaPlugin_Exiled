using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using SanyaPlugin.Functions;


namespace SanyaPlugin
{
	public sealed class Configs : IConfig
	{
		public Configs()
		{
			DataDirectory = Path.Combine(Paths.Configs, "SanyaPlugin");
		}

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

		[Description("各ロールの初期装備")]
		public Dictionary<string, string> Defaultitems { get; set; } = new Dictionary<string, string>()
		{
			{ RoleType.ClassD.ToString(), ItemType.None.ToString() }
		};
		public Dictionary<RoleType, List<ItemType>> DefaultitemsParsed = new Dictionary<RoleType, List<ItemType>>();

		[Description("Dクラスの脱出成功時装備")]
		public List<string> DefaultitemsEscapeClassd { get; set; } = new List<string>();
		public List<ItemType> DefaultitemsEscapeClassdParsed = new List<ItemType>();

		[Description("研究員のスポーン位置を変更する")]
		public bool ScientistsChangeSpawnPos { get; set; } = false;

		[Description("テスラで死亡した際の死体やアイテムを削除する")]
		public bool TeslaDeleteObjects { get; set; } = false;

		[Description("アイテムが自動で削除されるまでの秒数")]
		public int ItemCleanup { get; set; } = -1;

		[Description("アイテム削除の対象外アイテム")]
		public List<string> ItemCleanupIgnore { get; set; } = new List<string>();
		public List<ItemType> ItemCleanupIgnoreParsed = new List<ItemType>();

		[Description("Steamの制限付きユーザーをキックする")]
		public bool KickSteamLimited { get; set; } = false;

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

		[Description("核カウントダウンキャンセル時に全ドアを閉じる")]
		public bool CloseDoorsOnNukecancel { get; set; } = false;

		[Description("発電機のアンロックと同時にドアを開ける")]
		public bool GeneratorUnlockOpen { get; set; } = false;

		[Description("発電機の終了時にドアを閉じてロックする")]
		public bool GeneratorFinishLock { get; set; } = false;

		[Description("核の初期カウントダウン時間(30-120)")]
		public int WarheadInitCountdown { get; set; } = -1;

		[Description("核で地上ゲートを開放しない(自動核を除く)")]
		public bool WarheadDontOpenGates { get; set; } = false;

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

		[Description("ラウンド待機時のチュートリアルモード")]
		public bool WaitingTutorials { get; set; } = false;

		[Description("リスポーン場所をランダムにする")]
		public int RandomRespawnPosPercent { get; set; } = -1;

		[Description("Vキーチャットが可能なSCP（SCP-939以外）")]
		public List<string> AltvoicechatScps { get; set; } = new List<string>();
		public List<RoleType> AltvoicechatScpsParsed = new List<RoleType>();

		[Description("核起爆後の増援を停止する")]
		public bool StopRespawnAfterDetonated { get; set; } = false;

		[Description("インベントリ内のキーカードが効果を発揮するようになる")]
		public bool InventoryKeycardActivation { get; set; } = false;

		[Description("グレネードが命中するとヒットマークが出るように")]
		public bool HitmarkGrenade { get; set; } = false;

		[Description("キルすると大きいヒットマークが出るように")]
		public bool HitmarkKilled { get; set; } = false;

		[Description("裏切りの成功率")]
		public int TraitorChancePercent { get; set; } = 50;

		[Description("ジャンプで消費するスタミナ量")]
		public float StaminaCostJump { get; set; } = -1f;

		[Description("USPのダメージ乗数(対人間)")]
		public float UspDamageMultiplierHuman { get; set; } = 1f;

		[Description("USPのダメージ乗数(対SCP)")]
		public float UspDamageMultiplierScp { get; set; } = 1f;

		[Description("武装解除時の被ダメージ乗数")]
		public float CuffedDamageMultiplier { get; set; } = 1f;

		[Description("落下のダメージ乗数")]
		public float FalldamageMultiplier { get; set; } = 1f;

		[Description("SCP-914に入ると死亡する")]
		public bool Scp914Death { get; set; } = false;

		[Description("SCP-018のダメージ乗数")]
		public float Scp018DamageMultiplier { get; set; } = 1f;

		[Description("SCP-049の被ダメージ乗数")]
		public float Scp049DamageMultiplier { get; set; } = 1f;

		[Description("SCP-049の治療成功時回復量")]
		public int Scp049RecoveryAmount { get; set; } = 0;

		[Description("SCP-049の治療成功時追加AHP量")]
		public int Scp049CureAhpAmount { get; set; } = 0;

		[Description("SCP-049が殺害時に死体をスタックできるように")]
		public bool Scp049StackBody { get; set; } = false;

		[Description("SCP-049-2の被ダメージ乗数")]
		public float Scp0492DamageMultiplier { get; set; } = 1f;

		[Description("SCP-049-2のキル時回復量")]
		public int Scp0492RecoveryAmount { get; set; } = 0;

		[Description("SCP-049-2の攻撃にエフェクトを追加する")]
		public bool Scp0492AttackEffect { get; set; } = false;

		[Description("SCP-096の被ダメージ乗数")]
		public float Scp096DamageMultiplier { get; set; } = 1f;

		[Description("SCP-096のキル時回復量")]
		public int Scp096RecoveryAmount { get; set; } = 0;

		[Description("SCP-096の一人当たりの増加AHP量")]
		public int Scp096ShieldPerTargets { get; set; } = 70;

		[Description("SCP-096に触れると発狂するようになる距離")]
		public float Scp096TouchEnrageDistance { get; set; } = -1f;

		[Description("SCP-106の被ダメージ乗数")]
		public float Scp106DamageMultiplier { get; set; } = 1f;

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

		[Description("SCP-106のポータルを踏むとポケットディメンションへ飛ばされる")]
		public bool Scp106PocketTrap { get; set; } = false;

		[Description("SCP-173の被ダメージ乗数")]
		public float Scp173DamageMultiplier { get; set; } = 1f;

		[Description("SCP-173のキル時回復量")]
		public int Scp173RecoveryAmount { get; set; } = 0;

		[Description("SCP-173の被視認者に応じて増加するAHPの一人当たりの量")]
		public int Scp173SeeingByHumansAhpAmount { get; set; } = 0;

		[Description("SCP-173のまばたきのの最小間隔")]
		public float Scp173MinBlinktime { get; set; } = 2.5f;

		[Description("SCP-173のまばたきのの最大間隔")]
		public float Scp173MaxBlinktime { get; set; } = 3.5f;

		[Description("SCP-939-XXの被ダメージ乗数")]
		public float Scp939DamageMultiplier { get; set; } = 1f;

		[Description("SCP-939-XXのキル時回復量")]
		public int Scp939RecoveryAmount { get; set; } = 0;

		[Description("SCP-939-XXの攻撃に出血エフェクトを追加する")]
		public bool Scp939AttackBleeding { get; set; } = false;

		[Description("SCP-939-XXの攻撃で死体を発生させない")]
		public bool Scp939RemoveRagdoll { get; set; } = false;

		[Description("SCP-939-XXが人間の視認数に応じてAHPを持つ際の一人当たりの量")]
		public int Scp939SeeingAhpAmount { get; set; } = 0;

		[Description("SCP-939-XXがVC使用中の人間を視認できるように")]
		public bool Scp939CanSeeVoiceChatting { get; set; } = false;

		[Description("SCP-939-XXが擬態される距離")]
		public float Scp939FakeHumansRange { get; set; } = -1;

		[Description("SCP-079がゲートと914操作に必要なTier")]
		public int Scp079NeedInteractTierGateand914 { get; set; } = -1;

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

		[Description("SCP-079の1カメラ移動コスト")]
		public float Scp079CostCamera { get; set; } = 1f;

		[Description("SCP-079のドアロック維持コスト")]
		public float Scp079CostLock { get; set; } = 4f;

		[Description("SCP-079のドアロック初期コスト")]
		public float Scp079CostLockStart { get; set; } = 5f;

		[Description("SCP-079のドアロック必要最低量")]
		public float Scp079ConstLockMinimum { get; set; } = 10f;

		[Description("SCP-079のドア操作コスト(権限無しドア)")]
		public float Scp079CostDoorDefault { get; set; } = 5f;

		[Description("SCP-079のドア操作コスト(ContLv1)")]
		public float Scp079CostDoorContlv1 { get; set; } = 50f;

		[Description("SCP-079のドア操作コスト(ContLv2)")]
		public float Scp079CostDoorContlv2 { get; set; } = 40f;

		[Description("SCP-079のドア操作コスト(ContLv3)")]
		public float Scp079CostDoorContlv3 { get; set; } = 110f;

		[Description("SCP-079のドア操作コスト(ArmoryLv1)")]
		public float Scp079CostDoorArmlv1 { get; set; } = 50f;

		[Description("SCP-079のドア操作コスト(ArmoryLv2)")]
		public float Scp079CostDoorArmlv2 { get; set; } = 60f;

		[Description("SCP-079のドア操作コスト(ArmoryLv3)")]
		public float Scp079CostDoorArmlv3 { get; set; } = 70f;

		[Description("SCP-079のドア操作コスト(Exit<GateAB>)")]
		public float Scp079CostDoorExit { get; set; } = 60f;

		[Description("SCP-079のドア操作コスト(Intercom)")]
		public float Scp079CostDoorIntercom { get; set; } = 30f;

		[Description("SCP-079のドア操作コスト(Checkpoint)")]
		public float Scp079CostDoorCheckpoint { get; set; } = 10f;

		[Description("SCP-079のロックダウンコスト")]
		public float Scp079CostLockDown { get; set; } = 60f;

		[Description("SCP-079のテスラゲート使用コスト")]
		public float Scp079CostTesla { get; set; } = 50f;

		[Description("SCP-079の階層移動コスト")]
		public float Scp079CostElevatorTeleport { get; set; } = 30f;

		[Description("SCP-079のエレベーター操作コスト")]
		public float Scp079CostElevatorUse { get; set; } = 10f;

		[Description("SCP-079のスピーカー使用初期コスト")]
		public float Scp079CostSpeakerStart { get; set; } = 10f;

		[Description("SCP-079のスピーカー使用維持コスト")]
		public float Scp079CostSpeakerUpdate { get; set; } = 0.8f;

		public string GetConfigs()
		{
			string returned = "\n";

			PropertyInfo[] infoArray = typeof(Configs).GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach(PropertyInfo info in infoArray)
			{
				if(info.PropertyType.IsList())
				{
					var list = info.GetValue(this) as IEnumerable;
					returned += $"{info.Name}:\n";
					if(list != null)
						foreach(var i in list) returned += $"{i}\n";
				}
				else if(info.PropertyType.IsDictionary())
				{
					returned += $"{info.Name}: ";

					var obj = info.GetValue(this);

					IDictionary dict = (IDictionary)obj;

					var key = obj.GetType().GetProperty("Keys");
					var value = obj.GetType().GetProperty("Values");
					var keyObj = key.GetValue(obj, null);
					var valueObj = value.GetValue(obj, null);
					var keyEnum = keyObj as IEnumerable;

					foreach(var i in dict.Keys)
					{
						returned += $"[{i}:{dict[i]}]";
					}

					returned += "\n";
				}
				else
				{
					returned += $"{info.Name}: {info.GetValue(this)}\n";
				}
			}

			FieldInfo[] fieldInfos = typeof(Configs).GetFields(BindingFlags.Public | BindingFlags.Instance);

			foreach(var info in fieldInfos)
			{
				if(info.FieldType.IsList())
				{
					var list = info.GetValue(this) as IEnumerable;
					returned += $"{info.Name}:\n";
					if(list != null)
						foreach(var i in list) returned += $"{i}\n";
				}
				else if(info.FieldType.IsDictionary())
				{
					returned += $"{info.Name}: ";

					var obj = info.GetValue(this);

					IDictionary dict = (IDictionary)obj;

					var key = obj.GetType().GetProperty("Keys");
					var value = obj.GetType().GetProperty("Values");
					var keyObj = key.GetValue(obj, null);
					var valueObj = value.GetValue(obj, null);
					var keyEnum = keyObj as IEnumerable;

					foreach(var i in dict.Keys)
					{
						if(dict[i].GetType().IsList())
						{
							var list = dict[i] as IEnumerable;
							returned += $"[{i}:";
							if(list != null)
								foreach(var x in list) returned += $"{x},";
							returned += "]";
						}
						else
						{
							returned += $"[{i}:{dict[i]}]";
						}
					}

					returned += "\n";
				}
				else
				{
					returned += $"{info.Name}: {info.GetValue(this)}\n";
				}
			}

			return returned;
		}
		public void ParseConfig()
		{
			try
			{
				DefaultitemsParsed.Clear();
				DefaultitemsEscapeClassdParsed.Clear();
				ItemCleanupIgnoreParsed.Clear();
				AltvoicechatScpsParsed.Clear();

				foreach(var key in Defaultitems)
					if(Enum.TryParse(key.Key, out RoleType role))
						DefaultitemsParsed.Add(role, new List<ItemType>(key.Value.Split(',').Select((string x) => (ItemType)Enum.Parse(typeof(ItemType), x))));

				foreach(var item in DefaultitemsEscapeClassd)
					if(Enum.TryParse(item, out ItemType type))
						DefaultitemsEscapeClassdParsed.Add(type);

				foreach(var item in ItemCleanupIgnore)
					if(Enum.TryParse(item, out ItemType type))
						ItemCleanupIgnoreParsed.Add(type);

				foreach(var item in AltvoicechatScps)
					if(Enum.TryParse(item, out RoleType role))
						AltvoicechatScpsParsed.Add(role);
			}
			catch(Exception ex)
			{
				Log.Error($"[ParseConfig] Error : {ex}");
			}
		}
	}
}