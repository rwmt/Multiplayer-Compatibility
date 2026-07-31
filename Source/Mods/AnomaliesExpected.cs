using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Anomalies Expected by MrHydralisk</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3240752689"/>
[MpCompatFor("MrHydralisk.AnomaliesExpected")]
class AnomaliesExpected
{
    public AnomaliesExpected(ModContentPack mod)
    {
        // Hospital bed
        {
            var type = AccessTools.TypeByName("AnomaliesExpected.Comp_AnomalyHospitalBed");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 2, 3, 4, 5, 6);
            PatchingUtilities.PatchPushPopRand(AccessTools.Method(type, "Sign"));
        }

        // Pie
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BakingPies", "CompGetGizmosExtra", 1, 2, 3, 4, 5, 6, 7);

        // Beam
        {
            var type = AccessTools.TypeByName("AnomaliesExpected.Comp_BeamTarget");
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1, 2, 3, 4, 5, 6, 7, 8);
            MP.RegisterSyncDelegateLambda(type, "TargetLocation", 1);
        }

        // Blood pump 
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BloodPump", "CompGetGizmosExtra", 0);

        // Broken statue
        {
            MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BrokenStatue", "CompGetGizmosExtra", 0);
            var hediff = AccessTools.Method("AnomaliesExpected.HediffComp_ObservingStage:CompPostTick");
            MpCompat.harmony.Patch(hediff, prefix: new HarmonyMethod(ObservePawnsInstead));
        }

        // Destroyable
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_CanDestroyedAfterStudy", "CompGetGizmosExtra", 0);

        // Stockings
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_ChristmasStockings", "CompGetGizmosExtra", 1, 2, 3, 4, 5, 6, 7);

        // Meat Grinder
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_MeatGrinder", "CompGetGizmosExtra", 1, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15);

        // Speedometer
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_Speedometer", "CompGetGizmosExtra", 0);
        MpCompat.RegisterLambdaDelegate("AnomaliesExpected.Hediff_SpeedometerLevel", "GetGizmos", 1, 3, 4, 5);

        // Notepad
        MpCompat.RegisterLambdaDelegate("AnomaliesExpected.Comp_StudyNotepad", "CompGetGizmosExtra", 2);

        // Fleshbeast
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.CompAbilityEffect_FleshbeastCommand", "CompGetGizmosExtra", 1);

        // Clockwork
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.CompObelisk_Clockwork", "CompGetGizmosExtra", 0, 1, 2, 4, 6, 8, 9);
    }

    // Can't test area visibility across all player cameras in multiplayer
    // Replace it with pawn line of sight instead
    static bool ObservePawnsInstead(HediffComp __instance)
    {
        if (!MP.IsInMultiplayer) return true;

        const float distance = 8f;
        float newSeverity = 1f;

        var subject = __instance.parent.pawn;

        foreach (Pawn observer in subject.Map.mapPawns.AllHumanlikeSpawned.ToList())
        {
            if (observer.Position.InHorDistOf(subject.Position, distance) && GenSight.LineOfSightToThing(subject.Position, observer, subject.Map))
            {
                newSeverity = 0.5f;

                break;
            }
        }
        
        __instance.parent.Severity = newSeverity;

        return false;
    }
}
