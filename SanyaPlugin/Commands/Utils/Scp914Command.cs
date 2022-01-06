using System;
using CommandSystem;
using SanyaPlugin.Commands.Utils.Scp914;

namespace SanyaPlugin.Commands.Utils
{
	public class Scp914Command : ParentCommand, IUsageProvider
	{
		public Scp914Command() => LoadGeneratedCommands();

		public override string Command { get; } = "914";

		public override string[] Aliases { get; }

		public override string Description { get; } = "SCP-914に関する親コマンド";

		public string[] Usage { get; } = new string[] { "use/knob" };

		public string RequiredPermission { get; } = "sanya.utils.914";

		public override void LoadGeneratedCommands() 
		{
			RegisterCommand(new UseCommand());
			RegisterCommand(new KnobCommand());
		}

		protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			response = $"オプションが必要です:{this.DisplayCommandUsage()}";
			return false;
		}
	}
}
