using System;
using System.Collections.Generic;
using EXILED;
using UnityEngine;
using Utf8Json;

namespace SanyaPlugin
{
    public enum SANYA_GAME_MODE
    {
        NULL = -1,
        NORMAL = 0,
        NIGHT,
        CLASSD_INSURGENCY
    }

    public enum GRENADE_ID
    {
        FRAG_NADE = 0,
        FLASH_NADE = 1,
        SCP018_NADE = 2
    }

    public static class OutsideRandomAirbombPos
    {
        public static List<Vector3> Load()
        {
            return new List<Vector3>{
                new Vector3(UnityEngine.Random.Range(175, 182),  984, UnityEngine.Random.Range( 25,  29)),
                new Vector3(UnityEngine.Random.Range(174, 182),  984, UnityEngine.Random.Range( 36,  39)),
                new Vector3(UnityEngine.Random.Range(174, 182),  984, UnityEngine.Random.Range( 36,  39)),
                new Vector3(UnityEngine.Random.Range(166, 174),  984, UnityEngine.Random.Range( 26,  39)),
                new Vector3(UnityEngine.Random.Range(169, 171),  987, UnityEngine.Random.Range(  9,  24)),
                new Vector3(UnityEngine.Random.Range(174, 175),  988, UnityEngine.Random.Range( 10,  -2)),
                new Vector3(UnityEngine.Random.Range(186, 174),  990, UnityEngine.Random.Range( -1,  -2)),
                new Vector3(UnityEngine.Random.Range(186, 189),  991, UnityEngine.Random.Range( -1, -24)),
                new Vector3(UnityEngine.Random.Range(186, 189),  991, UnityEngine.Random.Range( -1, -24)),
                new Vector3(UnityEngine.Random.Range(185, 189),  993, UnityEngine.Random.Range(-26, -34)),
                new Vector3(UnityEngine.Random.Range(180, 195),  995, UnityEngine.Random.Range(-36, -91)),
                new Vector3(UnityEngine.Random.Range(148, 179),  995, UnityEngine.Random.Range(-45, -72)),
                new Vector3(UnityEngine.Random.Range(118, 148),  995, UnityEngine.Random.Range(-47, -65)),
                new Vector3(UnityEngine.Random.Range( 83, 118),  995, UnityEngine.Random.Range(-47, -65)),
                new Vector3(UnityEngine.Random.Range( 13,  15),  995, UnityEngine.Random.Range(-18, -48)),
                new Vector3(UnityEngine.Random.Range( 84,  88),  988, UnityEngine.Random.Range(-67, -70)),
                new Vector3(UnityEngine.Random.Range( 68,  83),  988, UnityEngine.Random.Range(-52, -66)),
                new Vector3(UnityEngine.Random.Range( 53,  68),  988, UnityEngine.Random.Range(-53, -63)),
                new Vector3(UnityEngine.Random.Range( 12,  49),  988, UnityEngine.Random.Range(-47, -66)),
                new Vector3(UnityEngine.Random.Range( 38,  42),  988, UnityEngine.Random.Range(-40, -47)),
                new Vector3(UnityEngine.Random.Range( 38,  43),  988, UnityEngine.Random.Range(-32, -38)),
                new Vector3(UnityEngine.Random.Range(-25,  12),  988, UnityEngine.Random.Range(-50, -66)),
                new Vector3(UnityEngine.Random.Range(-26, -56),  988, UnityEngine.Random.Range(-50, -66)),
                new Vector3(UnityEngine.Random.Range( -3, -24), 1001, UnityEngine.Random.Range(-66, -73)),
                new Vector3(UnityEngine.Random.Range(  5,  28), 1001, UnityEngine.Random.Range(-66, -73)),
                new Vector3(UnityEngine.Random.Range( 29,  55), 1001, UnityEngine.Random.Range(-66, -73)),
                new Vector3(UnityEngine.Random.Range( 50,  54), 1001, UnityEngine.Random.Range(-49, -66)),
                new Vector3(UnityEngine.Random.Range( 24,  48), 1001, UnityEngine.Random.Range(-41, -46)),
                new Vector3(UnityEngine.Random.Range(  5,  24), 1001, UnityEngine.Random.Range(-41, -46)),
                new Vector3(UnityEngine.Random.Range( -4, -17), 1001, UnityEngine.Random.Range(-41, -46)),
                new Vector3(UnityEngine.Random.Range(  4,  -4), 1001, UnityEngine.Random.Range(-25, -40)),
                new Vector3(UnityEngine.Random.Range( 11, -11), 1001, UnityEngine.Random.Range(-18, -21)),
                new Vector3(UnityEngine.Random.Range(  3,  -3), 1001, UnityEngine.Random.Range( -4, -17)),
                new Vector3(UnityEngine.Random.Range(  2,  14), 1001, UnityEngine.Random.Range(  3,  -3)),
                new Vector3(UnityEngine.Random.Range( -1, -13), 1001, UnityEngine.Random.Range(  4,  -3))
            };
    }
    }

    public class Serverinfo
    {
        public Serverinfo() { players = new List<Playerinfo>(); }

        public string time { get; set; }

        public string gameversion { get; set; }

        public string modversion { get; set; }

        public string sanyaversion { get; set; }

        public string gamemode { get; set; }

        public string name { get; set; }

        public string ip { get; set; }

        public int port { get; set; }

        public int playing { get; set; }

        public int maxplayer { get; set; }

        public int duration { get; set; }

        public List<Playerinfo> players { get; private set; }
    }

    public class Playerinfo
    {
        public Playerinfo() { }

        public string name { get; set; }

        public string userid { get; set; }

        public string ip { get; set; }

        public string role { get; set; }

        public string rank { get; set; }
    }

    public class PlayerData
    {
        public PlayerData(DateTime lastupdate, string userid, bool isSteamLimited, int level, int exp, int count)
        {
            this.lastUpdate = lastupdate;
            this.userid = userid;
            this.limited = isSteamLimited;
            this.level = level;
            this.exp = exp;
            this.playingcount = count;
        }

        public void AddExp(int amount)
        {
            if(string.IsNullOrEmpty(amount.ToString()) || exp == -1 || level == -1) return;

            Log.Debug($"[AddExp] Player:{userid} EXP:{exp} + {amount} = {exp + amount} ");

            int sum = exp + amount;

            //1*3 <= 10
            //2*3 <= 7
            //3*3 <= 1
            if(level * 3 <= sum)
            {
                while(level * 3 <= sum)
                {
                    exp = sum - level * 3;
                    sum -= level * 3;
                    level++;
                }
            }
            else
            {
                exp = sum;
            }
        }
        public override string ToString()
        {
            return $"userid:{userid} limited:{limited} level:{level} exp:{exp}";
        }

        public DateTime lastUpdate;
        public string userid;
        public bool limited;
        public int level;
        public int exp;
        public int playingcount;
    }

    public class WebhookData
    {
        public List<Embed> embeds { get; set; } = new List<Embed>();
    }

    public class Embed
    {
        public string title { get; set; }

        public string timestamp { get; set; }
 
        public EmbedFooter footer { get; set; } = new EmbedFooter();

        public List<EmbedField> fields { get; set; } = new List<EmbedField>();
    }

    public class EmbedAuthor
    {
        public string name { get; set; }
    }

    public class EmbedFooter
    {
        public string text { get; set; }
    }

    public class EmbedField
    {
        public string name { get; set; }
        public string value { get; set; }
        public bool inline { get; set; } = false;
    }

    //public class SCPPlayerData
    //{
    //    public SCPPlayerData(int i, string n, Role r, Vector p, int h, int l079 = -1, int a079 = -1, Vector c079 = null) { id = i; name = n; role = r; pos = p; health = h; level079 = l079; ap079 = a079; camera079 = c079; }

    //    public int id { get; set; }

    //    public string name { get; set; }

    //    public Role role { get; set; }

    //    public Vector pos { get; set; }

    //    public int health { get; set; }

    //    public int level079 { get; set; }

    //    public int ap079 { get; set; }

    //    public Vector camera079 { get; set; }
    //}

    //public class PlayerScoreInfo
    //{
    //    public PlayerScoreInfo(Player ply) { player = ply; killamount = 0; deathamount = 0; damageamount = 0; scpkillamount = 0; }

    //    public Player player { get; }

    //    public int killamount { get; set; }

    //    public int deathamount { get; set; }

    //    public int damageamount { get; set; }

    //    public int scpkillamount { get; set; }
    //}
}
