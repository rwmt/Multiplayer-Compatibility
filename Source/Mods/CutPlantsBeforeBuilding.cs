using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Cut plants before building by tammybee</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1539025677"/>
    [MpCompatFor("tammybee.cutplantsbeforebuilding")]
    [MpCompatFor("Mlie.CutPlantsBeforeBuilding")]
    class CutPlantsBeforeBuilding
    {
        private static ISyncField syncAutoCutPlants;

        public CutPlantsBeforeBuilding(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            syncAutoCutPlants = MP.RegisterSyncField(
                AccessTools.Field(AccessTools.TypeByName("CutPlantsBeforeBuilding.Main"), "AutoDesignatePlantsCutMode"));
            MpCompat.harmony.Patch(AccessTools.Method("CutPlantsBeforeBuilding.HarmonyPatches.PlaySettings_DoPlaySettingsGlobalControls:Prefix"),
                prefix: new HarmonyMethod(WatchAutoCutPlantsPrefix));
        }

        // No need to begin and end watching, as this prefix is already called in a place where it's done by the MP mod
        // (The place where auto expanding home area and auto rebuilding destroyed things is synced)
        private static void WatchAutoCutPlantsPrefix() => syncAutoCutPlants.Watch();
    }
}
