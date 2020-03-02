using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EXILED;
using EXILED.Extensions;
using MEC;
using Mirror;
using UnityEngine;

namespace SanyaPlugin
{
    internal static class PlayerDataManager
    {
        public static Dictionary<string, PlayerData> playersData = new Dictionary<string, PlayerData>();

        public static PlayerData LoadPlayerData(string userid)
        {
            string targetuseridpath = Path.Combine(SanyaPlugin.PlayersDataPath, $"{userid}.txt");
            if(!Directory.Exists(SanyaPlugin.PlayersDataPath)) Directory.CreateDirectory(SanyaPlugin.PlayersDataPath);
            if(!File.Exists(targetuseridpath)) return new PlayerData(DateTime.Now, userid, false, 0, 0, 0);
            else return ParsePlayerData(targetuseridpath);
        }

        public static void SavePlayerData(PlayerData data)
        {
            string targetuseridpath = Path.Combine(SanyaPlugin.PlayersDataPath, $"{data.userid}.txt");

            if(!Directory.Exists(SanyaPlugin.PlayersDataPath)) Directory.CreateDirectory(SanyaPlugin.PlayersDataPath);

            string[] textdata = new string[] {
                data.lastUpdate.ToString("yyyy-MM-ddTHH:mm:sszzzz"),
                data.userid,
                data.limited.ToString(),
                data.level.ToString(),
                data.exp.ToString(),
                data.playingcount.ToString()
            };

            File.WriteAllLines(targetuseridpath, textdata);
        }

        private static PlayerData ParsePlayerData(string path)
        {
            var text = File.ReadAllLines(path);
            return new PlayerData(
                DateTime.Parse(text[0]),
                text[1],
                bool.Parse(text[2]),
                int.Parse(text[3]),
                int.Parse(text[4]),
                int.Parse(text[5])
                );
        }
    }

    internal static class Coroutines
    {
        public static bool isAirBombGoing = false;
        public static readonly Dictionary<ReferenceHub, CoroutineHandle> DOTDamages = new Dictionary<ReferenceHub, CoroutineHandle>();

        public static IEnumerator<float> GrantedLevel(ReferenceHub player, PlayerData data)
        {
            yield return Timing.WaitForSeconds(1f);

            var group = player.serverRoles.Group?.Clone();
            string level = data.level.ToString();
            string rolestr = player.serverRoles.GetUncoloredRoleString();
            string rolecolor = player.serverRoles.MyColor;
            string badge;

            rolestr = rolestr.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

            if(rolecolor == "light_red")
            {
                rolecolor = "pink";
            }

            if(data.level == -1)
            {
                level = "?????";
            }

            if(string.IsNullOrEmpty(rolestr))
            {
                badge = $"Level{level}";
            }
            else
            {
                badge = $"Level{level} : {rolestr}";
            }

            if(group == null)
            {
                group = new UserGroup()
                {
                    BadgeText = badge,
                    BadgeColor = "default",
                    HiddenByDefault = false,
                    Cover = true,
                    KickPower = 0,
                    Permissions = 0,
                    RequiredKickPower = 0,
                    Shared = false
                };
            }
            else
            {
                group.BadgeText = badge;
                group.BadgeColor = rolecolor;
                group.HiddenByDefault = false;
                group.Cover = true;
            }

            player.serverRoles.SetGroup(group, false, false, true);

            Log.Debug($"[GrantedLevel] {player.GetUserId()} : Level{level}");

            yield break;
        }

        public static IEnumerator<float> StartNightMode()
        {
            Log.Debug($"[StartNightMode] Started. Wait for {60}s...");
            yield return Timing.WaitForSeconds(60f);
            if(Configs.cassie_subtitle)
            {
                Methods.SendSubtitle(Subtitles.StartNightMode, 20);
            }
            PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("warning . facility power system has been attacked . all most containment zones light does not available until generator activated .", false, true);
            Generator079.mainGenerator.RpcCustomOverchargeForOurBeautifulModCreators(10f, false);
            yield break;
        }

        public static IEnumerator<float> BigHitmark(MicroHID microHID)
        {
            yield return Timing.WaitForSeconds(0.1f);
            microHID.TargetSendHitmarker(false);
            yield break;
        }

        public static IEnumerator<float> AirSupportBomb(int waitforready = 5)
        {
            Log.Info($"[AirSupportBomb] booting...");
            if(isAirBombGoing)
            {
                Log.Info($"[Airbomb] already booted, cancel.");
                yield break;
            }
            else
            {
                isAirBombGoing = true;
            }

            if(Configs.cassie_subtitle)
            {
                Methods.SendSubtitle(Subtitles.AirbombStarting, 10);
                PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("danger . outside zone emergency termination sequence activated .", false, true);
                yield return Timing.WaitForSeconds(5f);
            }

            Log.Info($"[AirSupportBomb] charging...");
            while(waitforready > 0)
            {
                Methods.PlayAmbientSound(7);
                waitforready--;
                yield return Timing.WaitForSeconds(1f);
            }

            Log.Info($"[AirSupportBomb] throwing...");
            while(isAirBombGoing)
            {
                List<Vector3> randampos = OutsideRandomAirbombPos.pos;
                randampos.OrderBy(x => Guid.NewGuid()).ToList();
                foreach(var pos in randampos)
                {
                    Methods.Explode(pos, (int)GRENADE_ID.FRAG_NADE);
                    yield return Timing.WaitForSeconds(0.1f);
                }
                yield return Timing.WaitForSeconds(0.25f);
            }

            if(Configs.cassie_subtitle)
            {
                Methods.SendSubtitle(Subtitles.AirbombEnded, 10);
                PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("outside zone termination completed .", false, true);
            }

            Log.Info($"[AirSupportBomb] Ended.");
            yield break;
        }

        public static IEnumerator<float> DOTDamage(ReferenceHub target, int perDamage, int maxLimitDamage, float interval, DamageTypes.DamageType type)
        {
            int curDamageAmount = 0;
            Vector3 curDeathPos = target.characterClassManager.NetworkDeathPosition;
            RoleType curRole = target.GetRole();
            while(curDamageAmount < maxLimitDamage)
            {
                if(target.characterClassManager.NetworkDeathPosition != curDeathPos || target.GetRole() != curRole) break;
                target.playerStats.HurtPlayer(new PlayerStats.HitInfo(perDamage, "WORLD", type, 0), target.gameObject);
                maxLimitDamage += perDamage;
                yield return Timing.WaitForSeconds(interval);
            }
            yield break;
        }
    }

    internal static class Methods
    {
        static public void Explode(Vector3 position, int type, ReferenceHub player = null)
        {
            if(player == null) player = ReferenceHub.GetHub(PlayerManager.localPlayer);
            var gm = player.GetComponent<Grenades.GrenadeManager>();
            Grenades.Grenade component = UnityEngine.Object.Instantiate(gm.availableGrenades[type].grenadeInstance).GetComponent<Grenades.Grenade>();
            component.FullInitData(gm, position, Quaternion.Euler(component.throwStartAngle), Vector3.zero, component.throwAngularVelocity);
            NetworkServer.Spawn(component.gameObject);
        }

        static public int GetRandomIndexFromWeight(int[] list)
        {
            int sum = 0;

            foreach(int i in list)
            {
                if(i <= 0) continue;
                sum += i;
            }

            int random = UnityEngine.Random.Range(0, sum);
            for(int i = 0; i < list.Length; i++)
            {
                if(list[i] <= 0) continue;

                if(random < list[i])
                {
                    return i;
                }
                random -= list[i];
            }
            return -1;
        }

        static public void SendSubtitle(string text, uint time, bool monospaced = false)
        {
            Broadcast brd = PlayerManager.localPlayer.GetComponent<Broadcast>();
            brd.RpcClearElements();
            brd.RpcAddElement(text, time, monospaced);
        }

        static public void PlayAmbientSound(int id)
        {
            PlayerManager.localPlayer.GetComponent<AmbientSoundPlayer>().RpcPlaySound(Mathf.Clamp(id, 0, 31));
        }

        static public void SpawnRagdoll()
        {
            //UnityEngine.Object.FindObjectOfType<RagdollManager>().SpawnRagdoll(ev.Machine.output.position,
            //                                                   player.transform.rotation,
            //                                                   (int)player.GetRoleType(),
            //                                                   info,
            //                                                   false,
            //                                                   player.GetComponent<MirrorIgnorancePlayer>().PlayerId,
            //                                                   player.GetName(),
            //                                                   player.queryProcessor.PlayerId
            //                                                   );
        }
    }
}