using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Time of day switches by merthsoft</summary>
    /// <see href="https://github.com/merthsoft/time-of-day-switch"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=776114412"/>
    [MpCompatFor("Merthsoft.TimeOfDaySwitch")]
    class TimeOfDAySwitch
    {
        public TimeOfDAySwitch(ModContentPack mod)
        {
            MP.RegisterSyncMethod(AccessTools.Method("Merthsoft.TimerSwitches.TimeOfDaySwitch:SetState"));
        }
    }
}
