namespace SanyaPlugin.Data
{
	internal static class Subtitles
	{
		internal static readonly string LimitedKickMessage = string.Concat(
			"あなたのSteamIDは「制限付きユーザーアカウント」です。このサーバーは制限付きユーザーを許可していません。\n",
			"(Your Steam account is Limited User Account. This server doesn't allow Limited User Account.)"
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

		internal static readonly string StartNightMode = string.Concat(
			"<color=#ff0000><size=25>",
			"《警告。施設の電源供給システムが攻撃を受けました。\nほぼ全ての部屋の照明は、発電機が作動するまで利用できません。》\n",
			"</size><size=20>",
			"《Warning. Facility power system has been attacked. \nAll most containment zones light does not available until generator activated.》\n",
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
			"《5つ中{0}つ目の発電機の起動が完了しました。》\n",
			"</size><size=20>",
			"《{0} out of 5 generators activated. 》\n",
			"</size></color>"
		);

		internal static readonly string GeneratorComplete = string.Concat(
			"<color=#bbee00><size=25>",
			"《5つ中5つ目の発電機の起動が完了しました。\n全ての発電機が起動されました。最終再収容手順を開始します。\n重度収容区画は約一分後にオーバーチャージされます。》\n",
			"</size><size=20>",
			"《5 out of 5 generators activated.\nAll generators has been sucessfully engaged.\nFinalizing recontainment sequence.\nHeavy containment zone will overcharge in t-minus 1 minutes.》\n",
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

		internal static readonly string AirbombStarting = string.Concat(
			"<color=#ff0000><size=25>",
			"《警告。地上エリアの緊急終了シーケンスが開始されます。》\n",
			"</size><size=20>",
			"《Danger, Outside Zone emergency termination sequence activated.》\n",
			"</size></color>"
		);

		internal static readonly string AirbombEnded = string.Concat(
			"<color=#ff0000><size=25>",
			"《地上エリアの緊急終了シーケンスが完了しました。》\n",
			"</size><size=20>",
			"《Outside Zone emergency termination completed.》\n",
			"</size></color>"
		);

		internal static readonly string DecontEvent = string.Concat(
			"<color=#bbee00><size=25>",
			"《軽度収容区画の「除染」プロトコルが開始されました。対象区画のSCPオブジェクトは破壊されます。》\n",
			"</size><size=20>",
			"《Decontamination process for Light Containment Zone has been started. SCP subject in zone will be destroyed.》\n",
			"</size></color>"
		);

		internal static readonly string ClassDInsurgencyFirst = string.Concat(
			"<color=#ff0000><size=25>",
			"《警告。軽度収容区画の武器庫にてセキュリティ違反を検知しました。》\n",
			"</size><size=20>",
			"《Danger. Detected security warning in light containment zones armory.》\n",
			"</size></color>"
		);

		internal static readonly string AlreadyBreakFirst = string.Concat(
			"<color=#6c80ff><size=25>",
			"《通達。施設警備員がサイトに到着しました。\n生存中の職員は、警備員が貴方の場所へ到着するまで「標準避難プロトコル」の続行を推奨します。》\n",
			"</size><size=20>",
			"《Attention all personnel. Facility guards has entered the facility.\nAll remaining personnel are advised to proceed with standard evacuation protocols until an MTF squad reaches your destination.》\n",
			"</size></color>"
		);
	}

	internal static class HintTexts
	{
		internal static readonly string ExtendEnabled = string.Concat(
			"<color=#bbee00><size=25>",
			"拡張モードが有効になりました. スプリントキーで切り替えられます.\n",
			"</size><size=20>",
			"Extend mode Enabled. You can toggle by sprint keys.\n",
			"</size></color>"
		);

		internal static readonly string ExtendDisabled = string.Concat(
			"<size=25>",
			"拡張モードが無効になりました. スプリントキーで切り替えられます.\n",
			"</size><size=20>",
			"Extend mode Disabled. You can toggle by sprint keys.\n",
			"</size>"
		);

		internal static readonly string Extend079First = string.Concat(
			"<color=#bbee00><size=25>",
			"拡張モードを使用可能です. スプリントキーで切り替えられます.\nTier1では見ている部屋にいる生存者の場所を伝えることができます.\n",
			"</size><size=20>",
			"You can use Extend mode. You can toggle by sprint keys.\nYou can spot living humans on current watching room.\n",
			"</size></color>"
		);

		internal static readonly string Extend079Lv2 = string.Concat(
			"<color=#bbee00><size=25>",
			"Tier2になりました. 拡張モードにてカメラ操作でSCPのもとへカメラ移動ができます.\n",
			"</size><size=20>",
			"You got to Tier2. On Extend mode, You can move to SCPs by camera interacts.\n",
			"</size></color>"
		);

		internal static readonly string Extend079Lv3 = string.Concat(
			"<color=#bbee00><size=25>",
			"Tier3になりました. 拡張モードにて格納庫のスピーカーでAlphaWarheadを操作できるようになります.\n",
			"</size><size=20>",
			"You got to Tier3. On Extend mode, You can control AlphaWarhead by Silo-Speaker.\n",
			"</size></color>"
		);

		internal static readonly string Extend079Lv4 = string.Concat(
			"<color=#bbee00><size=25>",
			"Tier4になりました. 拡張モードにて停電マークを押すと部屋の特定位置で爆発を起こせるようになります.\n",
			"</size><size=20>",
			"You got to Tier3. On Extend mode, You can explode where press LockDown button.\n",
			"</size></color>"
		);

		internal static readonly string Extend079Lv5 = string.Concat(
			"<color=#bbee00><size=25>",
			"Tier5になりました. 拡張モードにて地上ゲートを操作することにより人間陣営の爆発物を起爆できます.\n",
			"</size><size=20>",
			"You got to Tier3. On Extend mode, You can explode on humans inventory press Gate Door button.\n",
			"</size></color>"
		);

		internal static readonly string Error079NotEnoughTier = string.Concat(
			"<color=#bbee00><size=25>",
			"このドアを操作するにはTierが足りません。\n",
			"</size><size=20>",
			"Not enough Tier on interact it.\n",
			"</size></color>"
		);

		internal static readonly string Extend049First = string.Concat(
			"<color=#bbee00><size=25>",
			"人間を殺害時に死体は収納され、スプリントキーで治療することができます。\n",
			"</size><size=20>",
			"When you killed humans, Corpse is stocked and can be cure with sprint key.\n",
			"</size></color>"
		);

		internal static readonly string Extend106First = string.Concat(
			"<color=#bbee00><size=25>",
			"拡張モードを使用可能です. スプリントキーで使用可能です.\n収容室の前に戻ることができます。\n",
			"</size><size=20>",
			"You can use Extend mode. Use by sprint keys.\nYou can return to containment room.\n",
			"</size></color>"
		);


		internal static readonly string Extend106First_Obsoleted = string.Concat(
			"<color=#bbee00><size=25>",
			"拡張モードを使用可能です. スプリントキーで使用可能です.\n壁を通り抜けて移動することができます。\n",
			"</size><size=20>",
			"You can use Extend mode. Use by sprint keys.\nYou can walkthrough on walls.\n",
			"</size></color>"
		);
	}
}