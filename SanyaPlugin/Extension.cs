using System.Threading.Tasks;
using EXILED;
using EXILED.Extensions;

namespace SanyaPlugin
{
    public static class Extensions
    {
        public static Task StartSender(this Task task)
        {
            return task.ContinueWith((x) => { Log.Error($"[Sender] {x}"); }, TaskContinuationOptions.OnlyOnFaulted);
        }
        public static bool IsHandCuffed(this ReferenceHub player) => player.handcuffs.CufferId != -1;
        public static string GetIpAddress(this ReferenceHub player) => player.characterClassManager.RequestIp;
    }
}
