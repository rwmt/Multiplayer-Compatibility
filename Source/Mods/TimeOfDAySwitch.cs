using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Time of day switches by merthsoft</summary>
    /// <see href="https://bitbucket.org/merthsoft/timeofdayswitch"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=776114412"/>
    [MpCompatFor("Merthsoft.TimeOfDaySwitches")]
    class TimeOfDAySwitch
    {
        public TimeOfDAySwitch(ModContentPack mod)
        {
            MP.RegisterSyncMethod(AccessTools.Method("TimerSwitches.TimeOfDaySwitch:SetState"));
        }
    }
}
