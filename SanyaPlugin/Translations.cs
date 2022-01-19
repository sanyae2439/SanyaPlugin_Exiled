using System.ComponentModel;
using Exiled.API.Interfaces;

namespace SanyaPlugin
{
	public sealed class Translations : ITranslation
	{
		[Description("制限付きユーザーの際のメッセージ")]
		public string LimitedKickMessage { get; set; } = "あなたのSteamIDは「制限付きユーザーアカウント」です。このサーバーは制限付きユーザーを許可していません。\n(Your Steam account is Limited User Account. This server doesn't allow Limited User Account.)";

		[Description("VACBAN履歴を検知した際のメッセージ")]
		public string VacBannedKickMessage { get; set; } = "あなたのSteamIDは「VAC 検出記録」があります。このサーバーはそれを許可していません。\n(Your Steam account has VAC Detected record. This server doesn't allow it.)";

		[Description("Steamプロフィールがないために制限付きユーザーをチェックできない際のメッセージ")]
		public string NoProfileKickMessage { get; set; } = "あなたのSteamIDはプロフィールが作成されていません。このサーバーはプロフィール無しのユーザーを許可していません。\n(Your Steam account does not have Profile. This server doesn't allow users without Profile.)";

		[Description("VPNまたはプロキシ経由の接続を検知した際のメッセージ")]
		public string VpnKickMessage { get; set; } = "VPNまたはプロキシ経由の接続を検出しました。このサーバーはそれらを許可していません。\n(VPN or Proxy connection detected. This server doesn't allow VPN and Proxy.)";

		[Description("VPNまたはプロキシ経由の接続を検知した際のメッセージ(PreAuth用)")]
		public string VpnPreauthKickMessage { get; set; } = "This server doesn't allow VPN and Proxy.";

		[Description("高いPingユーザーを検知した際のメッセージ")]
		public string PingLimittedMessage { get; set; } = "通信環境が不安定なため、サーバーからキックされました。\n(You has been kicked by Higher ping.)";

		[Description("停電モード時の初期メッセージ")]
		public string BlackoutInit { get; set; } = "<color=#ff0000><size=25>《警告。施設の電源供給システムが攻撃を受けました。\nほぼ全ての部屋の照明は、発電機が2つ作動するまで利用できません。》\n</size><size=20>《Warning. Facility power system has been attacked. \nAll most containment zones light does not available until 2 generator activated.》\n</size></color>";

		[Description("SCP-079でTier制限がある扉を操作した際のメッセージ")]
		public string Error079NotEnoughTier { get; set; } = "<color=#bbee00><size=25>このドアを操作するにはTierが足りません。\n</size><size=20>Not enough Tier on interact it.\n</size></color>";
	}
}
