using System;
using System.Collections.Generic;
using System.IO;
using EXILED;
using MEC;

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
        static public IEnumerator<float> GrantedLevel(ReferenceHub player, PlayerData data)
        {
            yield return Timing.WaitForSeconds(1f);

            var group = player.serverRoles.Group?.Clone();
            string level = data.level.ToString();
            string rolestr = player.serverRoles.GetUncoloredRoleString();
            string rolecolor = player.serverRoles.MyColor;
            string badge;

            if(rolestr.Contains("Patreon"))
            {
                rolestr = "SCPSLPatreon";
            }
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
    }
}
