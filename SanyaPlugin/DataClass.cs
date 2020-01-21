using System.Collections.Generic;

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
