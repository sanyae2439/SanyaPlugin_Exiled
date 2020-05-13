using System;
using System.Collections.Generic;
using System.Reflection;
using EXILED;

namespace SanyaPlugin
{
	internal static class Configs
	{
		//info and report
		internal static string infosender_ip;
		internal static int infosender_port;
		internal static string report_webhook;

		//Smod Emulation
		internal static int auto_warhead_start;
		internal static bool auto_warhead_start_lock;
		internal static Dictionary<RoleType, List<ItemType>> defaultitems;
		internal static List<int> tesla_triggerable_teams;
		internal static int ragdoll_cleanup;
		internal static int item_cleanup;
		internal static List<ItemType> item_cleanup_ignore;

		//SanyaPlugin
		internal static bool kick_steam_limited;
		internal static bool kick_vpn;
		internal static string kick_vpn_apikey;
		internal static string motd_message = "";
		internal static List<int> event_mode_weight;
		internal static bool cassie_subtitle;
		internal static bool tesla_triggerable_disarmed;
		internal static bool generator_unlock_to_open;
		internal static bool generator_finish_to_lock;
		internal static bool generator_activating_opened;
		internal static bool intercom_information;
		internal static int outsidezone_termination_time_after_nuke;
		internal static bool godmode_after_endround;
		internal static bool fix_doors_on_countdown;
		internal static bool fix_doors_on_countdown_decont;
		internal static bool disable_all_chat;	
		internal static bool disable_spectator_chat;
		internal static bool disable_chat_bypass_whitelist;

		//SanyaPlugin:Event
		internal static List<ItemType> classd_insurgency_classd_inventory;
		internal static List<int> classd_insurgency_classd_ammo;
		internal static List<ItemType> classd_insurgency_scientist_inventory;

		//SanyaPlugin:Data
		internal static bool data_enabled;
		internal static bool level_enabled;
		internal static int level_exp_kill;
		internal static int level_exp_death;
		internal static int level_exp_win;
		internal static int level_exp_other;

		//SanyaPlugin:Config
		internal static bool command_enabled;
		internal static int command_ratelimit;

		//Human:Balanced
		internal static bool stop_respawn_after_detonated;
		internal static bool check_prev_spawn_team;
		internal static bool inventory_keycard_act;
		internal static bool item_shoot_move;
		internal static bool grenade_shoot_fuse;
		internal static bool grenade_chain_sametiming;
		internal static bool grenade_hitmark;
		internal static bool kill_hitmark;
		internal static int traitor_limitter;
		internal static int traitor_chance_percent;

		//SCP:Balanced
		internal static bool scp_can_talk_to_humans;
		internal static bool scp018_friendly_fire;
		internal static float scp018_damage_multiplier;
		internal static bool scp049_reset_ragdoll_after_recall;
		internal static bool scp0492_faster_ondamage;
		internal static bool scp079_spot;
		internal static bool scp096_high_sensitive;
		internal static bool scp106_reduce_grenade;
		internal static int scp173_hurt_blink_percent;
		internal static bool scp939_faster_halfhealth;
		internal static int scp939_dot_damage;
		internal static int scp939_dot_damage_total;
		internal static int scp939_dot_damage_interval;
		internal static bool scp914_intake_death;

		//Damage/Recovery
		internal static float damage_usp_multiplier_human;
		internal static float damage_usp_multiplier_scp;
		internal static float damage_divisor_cuffed;
		internal static float damage_divisor_scp049;
		internal static float damage_divisor_scp0492;
		internal static float damage_divisor_scp096;
		internal static float damage_divisor_scp106;
		internal static float damage_divisor_scp173;
		internal static float damage_divisor_scp939;
		internal static int recovery_amount_scp049;
		internal static int recovery_amount_scp0492;
		internal static int recovery_amount_scp096;
		internal static int recovery_amount_scp106;
		internal static int recovery_amount_scp173;
		internal static int recovery_amount_scp939;

		internal static void Reload()
		{
			infosender_ip = Plugin.Config.GetString("sanya_infosender_ip", "none");
			infosender_port = Plugin.Config.GetInt("sanya_infosender_port", 37813);
			report_webhook = Plugin.Config.GetString("sanya_report_webhook", string.Empty);
			tesla_triggerable_teams = Plugin.Config.GetIntList("sanya_tesla_triggerable_teams");
			auto_warhead_start = Plugin.Config.GetInt("sanya_auto_warhead_start", -1);
			auto_warhead_start_lock = Plugin.Config.GetBool("sanya_auto_warhead_start_lock", false);
			defaultitems = new Dictionary<RoleType, List<ItemType>>
			{
				{ RoleType.ClassD, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_classd").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.Scientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_scientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.FacilityGuard, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_guard").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.NtfCadet, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_cadet").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.NtfLieutenant, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_lieutenant").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.NtfCommander, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_commander").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.NtfScientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ntfscientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) },
				{ RoleType.ChaosInsurgency, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ci").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })) }
			};
			ragdoll_cleanup = Plugin.Config.GetInt("sanya_ragdoll_cleanup", -1);
			item_cleanup = Plugin.Config.GetInt("sanya_item_cleanup", -1);
			item_cleanup_ignore = Plugin.Config.GetStringList("sanya_item_cleanup_ignore").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); });

			kick_steam_limited = Plugin.Config.GetBool("sanya_kick_steam_limited", false);
			kick_vpn = Plugin.Config.GetBool("sanya_kick_vpn", false);
			kick_vpn_apikey = Plugin.Config.GetString("sanya_kick_vpn_apikey", string.Empty);
			motd_message = Plugin.Config.GetString("sanya_motd_message", string.Empty);
			event_mode_weight = Plugin.Config.GetIntList("sanya_event_mode_weight");
			cassie_subtitle = Plugin.Config.GetBool("sanya_cassie_subtitle", false);
			tesla_triggerable_disarmed = Plugin.Config.GetBool("sanya_tesla_triggerable_disarmed", false);
			generator_unlock_to_open = Plugin.Config.GetBool("sanya_generator_unlock_to_open", false);
			generator_finish_to_lock = Plugin.Config.GetBool("sanya_generator_finish_to_lock", false);
			generator_activating_opened = Plugin.Config.GetBool("sanya_generator_activating_opened", false);
			intercom_information = Plugin.Config.GetBool("sanya_intercom_information", false);
			outsidezone_termination_time_after_nuke = Plugin.Config.GetInt("sanya_outsidezone_termination_time_after_nuke", -1);
			godmode_after_endround = Plugin.Config.GetBool("sanya_godmode_after_endround", false);
			fix_doors_on_countdown = Plugin.Config.GetBool("sanya_fix_doors_on_countdown", false);
			fix_doors_on_countdown_decont = Plugin.Config.GetBool("sanya_fix_doors_on_countdown_decont", false);
			disable_spectator_chat = Plugin.Config.GetBool("sanya_disable_spectator_chat", false);
			disable_all_chat = Plugin.Config.GetBool("sanya_disable_all_chat", false);
			disable_chat_bypass_whitelist = Plugin.Config.GetBool("sanya_disable_chat_bypass_whitelist", false);

			classd_insurgency_classd_inventory = Plugin.Config.GetStringList("sanya_classd_insurgency_classd_inventory").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); });
			classd_insurgency_classd_ammo = Plugin.Config.GetIntList("sanya_classd_insurgency_classd_ammo");
			classd_insurgency_scientist_inventory = Plugin.Config.GetStringList("sanya_classd_insurgency_scientist_inventory").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); });

			data_enabled = Plugin.Config.GetBool("sanya_data_enabled", false);
			level_enabled = Plugin.Config.GetBool("sanya_level_enabled", false);
			level_exp_kill = Plugin.Config.GetInt("sanya_level_exp_kill", 3);
			level_exp_death = Plugin.Config.GetInt("sanya_level_exp_death", 1);
			level_exp_win = Plugin.Config.GetInt("sanya_level_exp_win", 10);
			level_exp_other = Plugin.Config.GetInt("sanya_level_exp_other", 1);

			command_enabled = Plugin.Config.GetBool("sanya_command_enabled", false);
			command_ratelimit = Plugin.Config.GetInt("sanya_command_ratelimit", 20);

			stop_respawn_after_detonated = Plugin.Config.GetBool("sanya_stop_respawn_after_detonated", false);
			check_prev_spawn_team = Plugin.Config.GetBool("sanya_check_prev_spawn_team", false);
			inventory_keycard_act = Plugin.Config.GetBool("sanya_inventory_keycard_act", false);
			item_shoot_move = Plugin.Config.GetBool("sanya_item_shoot_move", false);
			grenade_shoot_fuse = Plugin.Config.GetBool("sanya_grenade_shoot_fuse", false);
			grenade_chain_sametiming = Plugin.Config.GetBool("sanya_grenade_chain_sametiming", false);
			grenade_hitmark = Plugin.Config.GetBool("sanya_grenade_hitmark", false);
			kill_hitmark = Plugin.Config.GetBool("sanya_kill_hitmark", false);
			traitor_limitter = Plugin.Config.GetInt("sanya_traitor_limitter", -1);
			traitor_chance_percent = Plugin.Config.GetInt("sanya_traitor_chance_percent", 50);

			scp_can_talk_to_humans = Plugin.Config.GetBool("sanya_scp_can_talk_to_humans", false);
			scp018_friendly_fire = Plugin.Config.GetBool("sanya_grenade_friendly_fire", false);
			scp018_damage_multiplier = Plugin.Config.GetFloat("sanya_scp018_damage_multiplier", 1f);
			scp049_reset_ragdoll_after_recall = Plugin.Config.GetBool("sanya_scp049_reset_ragdoll_after_recall", false);
			scp0492_faster_ondamage = Plugin.Config.GetBool("sanya_scp0492_faster_ondamage", false);
			scp079_spot = Plugin.Config.GetBool("sanya_scp079_spot", false);
			scp096_high_sensitive = Plugin.Config.GetBool("sanya_scp096_high_sensitive", false);
			scp106_reduce_grenade = Plugin.Config.GetBool("sanya_scp106_reduce_grenade", false);
			scp173_hurt_blink_percent = Plugin.Config.GetInt("sanya_scp173_hurt_blink_percent", -1);
			scp939_faster_halfhealth = Plugin.Config.GetBool("sanya_scp939_faster_halfhealth", false);
			scp939_dot_damage = Plugin.Config.GetInt("sanya_scp939_dot_damage", -1);
			scp939_dot_damage_total = Plugin.Config.GetInt("sanya_scp939_dot_damage_total", 80);
			scp939_dot_damage_interval = Plugin.Config.GetInt("sanya_scp939_dot_damage_interval", 1);
			scp914_intake_death = Plugin.Config.GetBool("sanya_scp914_intake_death", false);

			damage_usp_multiplier_human = Plugin.Config.GetFloat("sanya_damage_usp_multiplier_human", 1.0f);
			damage_usp_multiplier_scp = Plugin.Config.GetFloat("sanya_damage_usp_multiplier_scp", 1.0f);
			damage_divisor_cuffed = Plugin.Config.GetFloat("sanya_damage_divisor_cuffed", 1.0f);
			damage_divisor_scp049 = Plugin.Config.GetFloat("sanya_damage_divisor_scp049", 1.0f);
			damage_divisor_scp0492 = Plugin.Config.GetFloat("sanya_damage_divisor_scp0492", 1.0f);
			damage_divisor_scp096 = Plugin.Config.GetFloat("sanya_damage_divisor_scp096", 1.0f);
			damage_divisor_scp106 = Plugin.Config.GetFloat("sanya_damage_divisor_scp106", 1.0f);
			damage_divisor_scp173 = Plugin.Config.GetFloat("sanya_damage_divisor_scp173", 1.0f);
			damage_divisor_scp939 = Plugin.Config.GetFloat("sanya_damage_divisor_scp939", 1.0f);
			recovery_amount_scp049 = Plugin.Config.GetInt("sanya_recovery_amount_scp049", -1);
			recovery_amount_scp0492 = Plugin.Config.GetInt("sanya_recovery_amount_scp0492", -1);
			recovery_amount_scp096 = Plugin.Config.GetInt("sanya_recovery_amount_scp096", -1);
			recovery_amount_scp106 = Plugin.Config.GetInt("sanya_recovery_amount_scp106", -1);
			recovery_amount_scp173 = Plugin.Config.GetInt("sanya_recovery_amount_scp173", -1);
			recovery_amount_scp939 = Plugin.Config.GetInt("sanya_recovery_amount_scp939", -1);

			Log.Info("[SanyaPluginConfig] Reloaded!");
		}

		internal static string GetConfigs()
		{
			string returned = "\n";

			FieldInfo[] infoArray = typeof(Configs).GetFields(BindingFlags.Static | BindingFlags.NonPublic);

			foreach(FieldInfo info in infoArray)
			{
				returned += $"{info.Name}: {info.GetValue(null)}\n";
			}

			return returned;
		}
	}
}