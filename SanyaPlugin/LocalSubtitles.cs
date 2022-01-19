﻿namespace SanyaPlugin.Data
{
	internal static class LocalSubtitles
	{
		internal static readonly string LimitedKickMessage = string.Concat(
			"あなたのSteamIDは「制限付きユーザーアカウント」です。このサーバーは制限付きユーザーを許可していません。\n",
			"(Your Steam account is Limited User Account. This server doesn't allow Limited User Account.)"
			);

		internal static readonly string VacBannedKickMessage = string.Concat(
			"あなたのSteamIDは「VAC 検出記録」があります。このサーバーはそれを許可していません。\n",
			"(Your Steam account has VAC Detected record. This server doesn't allow it.)"
			);

		internal static readonly string NoProfileKickMessage = string.Concat(
			"あなたのSteamIDはプロフィールが作成されていません。このサーバーはプロフィール無しのユーザーを許可していません。\n",
			"(Your Steam account does not have Profile. This server doesn't allow users without Profile.)"
			);

		internal static readonly string VPNKickMessage = string.Concat(
			"VPNまたはプロキシ経由の接続を検出しました。このサーバーはそれらを許可していません。\n",
			"(VPN or Proxy connection detected. This server doesn't allow VPN and Proxy.)"
			);
		internal static readonly string VPNKickMessageShort = string.Concat(
			"This server doesn't allow VPN and Proxy."
			);

		internal static readonly string PingLimittedMessage = string.Concat(
			"通信環境が不安定なため、サーバーからキックされました。\n",
			"(You has been kicked by Higher ping.)"
		);
		internal static readonly string MTFRespawnSCPs = string.Concat(
			"<color=#6c80ff><size=25>",
			"《機動部隊イプシロン-11「{0}」が施設に到着しました。\n残りの全職員は、機動部隊が貴方の場所へ到着するまで「標準避難プロトコル」の続行を推奨します。\n「{1}」オブジェクトが再収容されていません。》\n",
			"</size><size=20>",
			"《Mobile Task Force Unit, Epsilon-11, designated, '{0}', has entered the facility.\nAll remaining personnel are advised to proceed with standard evacuation protocols until an MTF squad reaches your destination.\nAwaiting recontainment of: {1} SCP subject.》\n",
			"</size></color>"
		);

		internal static readonly string MTFRespawnNOSCPs = string.Concat(
			"<color=#6c80ff><size=25>",
			"《機動部隊イプシロン-11「{0}」が施設に到着しました。\n残りの全職員は、機動部隊が貴方の場所へ到着するまで「標準避難プロトコル」の続行を推奨します。\n重大な脅威が施設内に存在します。注意してください。》\n",
			"</size><size=20>",
			"《Mobile Task Force Unit, Epsilon-11, designated, '{0}', has entered the facility.\nAll remaining personnel are advised to proceed with standard evacuation protocols, until MTF squad has reached your destination.\nSubstantial threat to safety is within the facility -- Exercise caution.》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathTesla = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}は自動セキュリティシステムによって無力化されました。{-1}》\n",
			"</size><size=20>",
			"《{0} successfully terminated by automatic security system.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathDecont = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}は「再収容プロトコル」によって無力化されました。{-1}》\n",
			"</size><size=20>",
			"《{0} lost in decontamination sequence.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathWarhead = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}はAlphaWarheadによって無力化されました。{-1}》\n",
			"</size><size=20>",
			"《{0} terminated by alpha warhead.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathTerminated = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}は{1}によって無力化されました。{-1}》\n",
			"</size><size=20>",
			"《{0} terminated by {2}.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathContainedMTF = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}の収容に成功しました。収容した部隊は{1}です。{-1}》\n",
			"</size><size=20>",
			"《{0} contained successfully. Containment unit:{1}.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string SCPDeathUnknown = string.Concat(
			"<color=#ff0000><size=25>",
			"《{0}の無力化に成功しました。無力化された原因は不明です。{-1}》\n",
			"</size><size=20>",
			"《{0} successfully terminated. Termination cause unspecified.{-2}》\n",
			"</size></color>"
		);

		internal static readonly string DecontaminationInit = string.Concat(
			"<color=#bbee00><size=25>",
			"《全ての職員へ通達。軽度収容区画の「除染」プロトコルが15分以内に実施されます。\n対象区画の生体物質は、破滅を避けるためにすべて除去されます。》\n",
			"</size><size=20>",
			"《Attention, all personnel, the Light Containment Zone decontamination process will occur in t-minus 15 minutes. \nAll biological substances must be removed in order to avoid destruction.》\n",
			"</size></color>"
		);

		internal static readonly string DecontaminationMinutesCount = string.Concat(
			"<color=#bbee00><size=25>",
			"《警告。軽度収容区画の「除染」プロトコルが{0}分以内に実施されます。》\n",
			"</size><size=20>",
			"《Danger, Light Containment zone overall decontamination in T-minus {0} Minutes.》\n",
			"</size></color>"
		);
		internal static readonly string Decontamination30s = string.Concat(
			"<color=#ff0000><size=25>",
			"《警告。軽度収容区画の「除染」プロトコルが30秒以内に実施されます。\n軽度収容区画のチェックポイントが完全に開放されます。すぐに避難してください。》\n",
			"</size><size=20>",
			"《Danger, Light Containment Zone overall decontamination in T-minus 30 seconds.\nAll checkpoint doors have been permanently opened. Please evacuate immediately.》\n",
			"</size></color>"
		);
		internal static readonly string DecontaminationLockdown = string.Concat(
			"<color=#bbee00><size=25>",
			"《軽度収容区画が「除染」プロトコルのために封鎖されました。生体反応の除去が開始されます。》\n",
			"</size><size=20>",
			"《Light Containment Zone is locked down and ready for decontamination. The removal of organic substances has now begun.》\n",
			"</size></color>"
		);

		internal static readonly string GeneratorFinish = string.Concat(
			"<color=#bbee00><size=25>",
			"《3つ中{0}つ目の発電機の起動が完了しました。》\n",
			"</size><size=20>",
			"《{0} out of 3 generators activated. 》\n",
			"</size></color>"
		);

		internal static readonly string GeneratorComplete = string.Concat(
			"<color=#bbee00><size=25>",
			"《3つ中3つ目の発電機の起動が完了しました。全ての発電機が起動されました。》\n",
			"</size><size=20>",
			"《3 out of 3 generators activated. All generators has been sucessfully engaged.》\n",
			"</size></color>"
		);

		internal static readonly string AlphaWarheadStart = string.Concat(
			"<color=#ff0000><size=25>",
			"《「AlphaWarhead」の緊急起爆シーケンスが開始されました。\n施設の地下区画は、約{0}秒後に爆破されます。》\n",
			"</size><size=20>",
			"《Alpha Warhead emergency detonation sequence engaged.\nThe underground section of the facility will be detonated in t-minus {0} seconds.》\n",
			"</size></color>"
		);

		internal static readonly string AlphaWarheadResume = string.Concat(
			"<color=#ff0000><size=25>",
			"《緊急起爆シーケンスが再開されました。約{0}秒後に爆破されます。》\n",
			"</size><size=20>",
			"《Detonation sequence resumed. t-minus {0} seconds.》\n",
			"</size></color>"
		);

		internal static readonly string AlphaWarheadCancel = string.Concat(
			"<color=#ff0000><size=25>",
			"《起爆が取り消されました。システムを再起動します。》\n",
			"</size><size=20>",
			"《Detonation cancelled. Restarting systems.》\n",
			"</size></color>"
		);

		internal static readonly string DecontEvent = string.Concat(
			"<color=#bbee00><size=25>",
			"《軽度収容区画の「除染」プロトコルが開始されました。対象区画のSCPオブジェクトは破壊されます。》\n",
			"</size><size=20>",
			"《Decontamination process for Light Containment Zone has been started. SCP subject in zone will be destroyed.》\n",
			"</size></color>"
		);
	}

	internal static class EventTexts
	{
		internal static readonly string BlackoutInit = string.Concat(
			"<color=#ff0000><size=25>",
			"《警告。施設の電源供給システムが攻撃を受けました。\nほぼ全ての部屋の照明は、発電機が2つ作動するまで利用できません。》\n",
			"</size><size=20>",
			"《Warning. Facility power system has been attacked. \nAll most containment zones light does not available until 2 generator activated.》\n",
			"</size></color>"
		);
	}

	internal static class HintTexts
	{
		internal static readonly string Error079NotEnoughTier = string.Concat(
			"<color=#bbee00><size=25>",
			"このドアを操作するにはTierが足りません。\n",
			"</size><size=20>",
			"Not enough Tier on interact it.\n",
			"</size></color>"
		);
	}
}