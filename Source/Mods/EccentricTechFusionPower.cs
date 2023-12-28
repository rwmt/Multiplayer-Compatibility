using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Eccentric Tech - Fusion Power by Aelanna</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2742125879"/>
    [MpCompatFor("Aelanna.EccentricTech.FusionPower")]
    internal class EccentricTechFusionPower
    {
        public EccentricTechFusionPower(ModContentPack mod)
        {
            // RNG
            {
                var type = AccessTools.TypeByName("EccentricPower.CompFusionGenerator");
                PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "generateNewFailurePoints"), false);
                PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "doEmergencyVentFireDamage"), true);
            }

            // Gizmos
            {
                var type = AccessTools.TypeByName("EccentricPower.CompFusionGenerator");
                MP.RegisterSyncMethod(type, "SetDesiredOutputLevel");
                MP.RegisterSyncMethod(type, "setPowerMode");
                MP.RegisterSyncMethod(type, "StartIgnitionCycle");
                MP.RegisterSyncMethod(type, "StartShutdownCycle");
                MP.RegisterSyncMethod(type, "StartEmergencyVent");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", Enumerable.Range(3, 5).ToArray()).SetDebugOnly(); // 3 to 7
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch()
        {
            var type = AccessTools.TypeByName("EccentricPower.CompFusionCapacitor");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 1, 2).SetDebugOnly();

            type = AccessTools.TypeByName("EccentricPower.CompFusionStorage");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1, 3, 4, 5, 6).Skip(2).SetDebugOnly();
        }
    }
}
