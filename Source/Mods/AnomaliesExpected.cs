using Verse;

namespace Multiplayer.Compat;

/// <summary>Anomalies Expected by MrHydralisk</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3240752689"/>
[MpCompatFor("MrHydralisk.AnomaliesExpected")]
class AnomaliesExpected
{
    public AnomaliesExpected(ModContentPack mod)
    {
        // Pie
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BakingPies", "CompGetGizmosExtra", 1, 2, 3, 4, 5, 6, 7);

        // Beam
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BeamTarget", "CompGetGizmosExtra", 1, 2, 3, 4, 5, 6, 7, 8);

        // Blood pump 
        MpCompat.RegisterLambdaMethod("AnomaliesExpected.Comp_BloodPump", "CompGetGizmosExtra", 0);

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
}
