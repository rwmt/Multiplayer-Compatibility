using System.Collections;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Smarter Construction by Hultis</summary>
    /// <see href="https://github.com/dhultgren/rimworld-smarter-construction"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773"/>
    [MpCompatFor("dhultgren.smarterconstruction")]
    class SmarterConstruction
    {
        // Patch_WorkGiver_Scanner_GetPriority
        private static AccessTools.FieldRef<IDictionary> workGiverScannerCacheField;
        // ClosedRegionDetector
        private static AccessTools.FieldRef<object> closedRegionDetectorCacheField;
        // EncloseThingsCache
        private static AccessTools.FieldRef<object, IDictionary> encloseThingsCacheField;
        // PawnPositionCache
        private static FieldInfo positionsCacheField;
        private static FieldInfo lastPositionsCacheCleanupField;
        // WorkGiver_ConstructFinishFrames_JobOnThing
        private static AccessTools.FieldRef<int> constructFinishFrameCurrentTickField;

        public SmarterConstruction(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("SmarterConstruction.Patches.Patch_WorkGiver_Scanner_GetPriority");

            // RNG
            {
                var field = AccessTools.Field(type, "random");

                field.SetValue(null, PatchingUtilities.RandRedirector.Instance);
            }

            // Cache
            {
                workGiverScannerCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(AccessTools.DeclaredField(type, "cache"));

                closedRegionDetectorCacheField = AccessTools.StaticFieldRefAccess<object>(
                    AccessTools.DeclaredField("SmarterConstruction.Core.ClosedRegionDetector:cache"));
                encloseThingsCacheField = AccessTools.FieldRefAccess<IDictionary>("SmarterConstruction.Core.EncloseThingsCache:cache");

                type = AccessTools.TypeByName("SmarterConstruction.Core.PawnPositionCache");
                // Harmony seems to fail to create FieldRef<T> on those 2, so just gonna use FieldInfo instead.
                positionsCacheField = AccessTools.DeclaredField(type, "positionCache");
                lastPositionsCacheCleanupField = AccessTools.DeclaredField(type, "lastCacheCleanup");

                constructFinishFrameCurrentTickField = AccessTools.StaticFieldRefAccess<int>(
                    AccessTools.DeclaredField("SmarterConstruction.Patches.WorkGiver_ConstructFinishFrames_JobOnThing:currentTick"));

                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                    postfix: new HarmonyMethod(typeof(SmarterConstruction), nameof(ClearCache)));
            }
        }

        private static void ClearCache()
        {
            workGiverScannerCacheField().Clear();
            // ClosedRegionDetector.cache.cache.Clear();
            encloseThingsCacheField(closedRegionDetectorCacheField()).Clear();

            ((IDictionary)positionsCacheField.GetValue(null)).Clear();
            // Reset the last cleanup tick field, as if we load an older save - it'll have to wait until the previously assigned tick to cleanup.
            lastPositionsCacheCleanupField.SetValue(null, 0);

            // Small chance it could cause issues when the loaded game is on the same tick as the tick stored by this field
            constructFinishFrameCurrentTickField() = -1;
            // Since we set the current tick to -1 (as opposed to 0), there's no chance unless something
            // is very wrong for `thingsUpdatedThisTick` field to be valid and kept by the mod. No need to clean it.
        }
    }
}
