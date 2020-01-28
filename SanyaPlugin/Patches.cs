using EXILED;
using Harmony;
using UnityEngine;

namespace SanyaPlugin
{
    [HarmonyPatch(typeof(Generator079), nameof(Generator079.Interact))]
    public class Generator079Interact
    {
        public static bool Prefix(Generator079 __instance, ref GameObject person, ref string command, ref float ___localTime)
        {
            Plugin.Debug($"[Harmony:Generator079Interact] {ReferenceHub.GetHub(person).GetName()}:{command}");

            if(command.StartsWith("EPS_DOOR"))
            {
                Plugin.Debug($"[OnGeneratorAccess] {ReferenceHub.GetHub(person).GetName()}:{ReferenceHub.GetHub(person).GetRoleType()}");
                AccessTools.Method(typeof(Generator079), "OpenClose", new System.Type[] {typeof(GameObject) }).Invoke(__instance,new object[] { person });
            }
            else if(command.StartsWith("EPS_TABLET"))
            {
                if(!__instance.isTabletConnected && __instance.isDoorOpen && ___localTime > 0f && !Generator079.mainGenerator.forcedOvercharge)
                {
                    Plugin.Debug($"[OnGeneratorTabletInsert] {ReferenceHub.GetHub(person).GetName()}:{ReferenceHub.GetHub(person).GetRoleType()}");

                    Inventory component = person.GetComponent<Inventory>();
                    if(!person.GetComponent<ServerRoles>().BypassMode)
                    {
                        foreach(Inventory.SyncItemInfo item in component.items)
                        {
                            if(item.id == ItemType.WeaponManagerTablet)
                            {
                                component.items.Remove(item);
                                __instance.NetworkisTabletConnected = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        __instance.NetworkisTabletConnected = true;
                    }
                }
            }
            else if(command.StartsWith("EPS_CANCEL"))
            {
                Plugin.Debug($"{SanyaPluginConfig.generator_eject_teams.Count}");
                foreach(var i in SanyaPluginConfig.generator_eject_teams)
                {
                    Plugin.Debug($"{(int)Plugin.GetTeam(ReferenceHub.GetHub(person).GetRoleType())}:{i}");
                }

                if(SanyaPluginConfig.generator_eject_teams.Count == 0 
                    || SanyaPluginConfig.generator_eject_teams.Contains((int)Plugin.GetTeam(ReferenceHub.GetHub(person).GetRoleType())))
                {
                    Plugin.Debug($"[OnGeneratorEject] {ReferenceHub.GetHub(person).GetName()}:{ReferenceHub.GetHub(person).GetRoleType()}");
                    __instance.EjectTablet();
                }
            }

            return false;
        }
    }
}
