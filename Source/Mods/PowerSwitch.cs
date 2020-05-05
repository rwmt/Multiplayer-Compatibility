using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{

    /// <summary>PowerSwitch by Haplo</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=717632155"/>
    /// <see href="https://github.com/HaploX1/RimWorld-PowerSwitch"/>
    [MpCompatFor("Haplo.PowerSwitch")]
    public class PowerSwitch
    {
        public PowerSwitch(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("PowerSwitch.Building_PowerSwitchMod");

            MP.RegisterSyncMethod(type, "switchPowerOnOff");
            MP.RegisterSyncMethod(type, "SwitchEnemyOnActiveOnOff");
            MP.RegisterSyncMethod(type, "SwitchEnemyOffActiveOnOff");
            MP.RegisterSyncMethod(type, "SwitchPawnActiveOnOff");
            MP.RegisterSyncMethod(type, "TimerOffClicked");
            MP.RegisterSyncMethod(type, "TimerOnClicked");
        }
    }
}
