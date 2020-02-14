using System;
using System.Collections.Generic;
using EXILED;
using UnityEngine;
using Utf8Json;

namespace SanyaPlugin
{
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

            Log.Debug($"[AddExp] Player:{userid} EXP:{exp} + {amount} = {exp+amount} ");

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
