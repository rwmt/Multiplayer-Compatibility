using System;
using Harmony;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Power Logic by Supes</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=812653710"/>
    /// <remarks>Modder implemented it on his own, congrats!</remarks>
    [MpCompatFor("Power Logic")]
    public class PowerLogicCompat
    {
        public PowerLogicCompat(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateLoad);
        }

        // PowerLogic requires late loading as some of the Types have statics that are not ready early.
        void LateLoad()
        {
            Type type;

            type = AccessTools.TypeByName("PowerLogic.Building_BaseLogicGate");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__14_0");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__14_2");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__14_4");

            type = AccessTools.TypeByName("PowerLogic.Building_CurrentSensor");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__15_1");

            type = AccessTools.TypeByName("PowerLogic.Building_HeatSensor");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_0");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_1");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_2");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_3");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_4");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_6");

            type = AccessTools.TypeByName("PowerLogic.Building_LightSensor");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_0");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_1");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__30_3");

            type = AccessTools.TypeByName("PowerLogic.Building_StackSensor");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_0");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_1");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_2");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_3");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_4");
            MP.RegisterSyncMethod(type, "<GetGizmos>b__19_5");

            type = AccessTools.TypeByName("PowerLogic.CompIFF");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__5_1");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__5_3");

            type = AccessTools.TypeByName("PowerLogic.CompProxSensor");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__19_0");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__19_1");

            type = AccessTools.TypeByName("PowerLogic.CompTransceiver");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__46_0");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__46_1");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__46_2");
            MP.RegisterSyncMethod(type, "<CompGetGizmosExtra>b__46_3");

            type = AccessTools.TypeByName("PowerLogic.ITab_Entanglement");
            MP.RegisterSyncDelegate(type, "<>c__DisplayClass3_1", "<FillTab>b__2");

            // TODO: Need to transpile or pester modder to move his click actions into methods:
            // ITab_DoorMacro
            // ITab_ThingDirections
            // ITab_WifiPulser
        }
    }
}
