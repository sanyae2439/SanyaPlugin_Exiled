using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SanyaPlugin
{
    public static class Extensions
    {
        public static string GetName(this ReferenceHub player) => player.nicknameSync.MyNick;
        public static string GetIpAddress(this ReferenceHub player) => player.characterClassManager.RequestIp;
        public static string GetUserId(this ReferenceHub player) => player.characterClassManager.UserId;
        public static RoleType GetRoleType(this ReferenceHub player) => player.characterClassManager.CurClass;
    }
}
