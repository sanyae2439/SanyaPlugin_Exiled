using System;
using System.Collections.Generic;
using EXILED;
using Harmony;

namespace SanyaPlugin
{
    internal static class SanyaPluginConfig
    {
        internal static string infosender_ip;
        internal static int infosender_port;
        internal static Dictionary<RoleType, List<ItemType>> defaultitems;
        internal static List<int> generator_eject_teams;
        internal static List<int> tesla_triggerable_teams;
        internal static bool tesla_triggerable_disarmed;

        internal static void Reload()
        {
            infosender_ip = Plugin.Config.GetString("sanya_infosender_ip", "hatsunemiku24.ddo.jp");
            infosender_port = Plugin.Config.GetInt("sanya_infosender_port", 37813);
            generator_eject_teams = new List<int>(Plugin.Config.GetIntList("sanya_generator_eject_teams"));
            tesla_triggerable_teams = new List<int>(Plugin.Config.GetIntList("sanya_tesla_triggerable_teams"));
            tesla_triggerable_disarmed = Plugin.Config.GetBool("sanya_tesla_triggerable_disarmed",true);

            defaultitems = new Dictionary<RoleType, List<ItemType>>();
            defaultitems.Add(RoleType.ClassD, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_classd").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.Scientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_scientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.FacilityGuard, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_guard").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.NtfCadet, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_cadet").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.NtfLieutenant, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_lieutenant").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.NtfCommander, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_commander").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.NtfScientist, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ntfscientist").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
            defaultitems.Add(RoleType.ChaosInsurgency, new List<ItemType>(Plugin.Config.GetStringList("sanya_defaultitem_ci").ConvertAll((string x) => { return (ItemType)Enum.Parse(typeof(ItemType), x); })));
        }
    }
}
