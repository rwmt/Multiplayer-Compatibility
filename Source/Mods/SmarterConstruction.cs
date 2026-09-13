using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Smarter Construction by Hultis</summary>
/// <see href="https://github.com/dhultgren/rimworld-smarter-construction"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2202185773"/>
[MpCompatFor("dhultgren.smarterconstruction")]
internal class SmarterConstruction
{
    // Patch_WorkGiver_Scanner_GetPriority
    private static AccessTools.FieldRef<IDictionary> workGiverScannerCacheField;

    // Pre-1.6 static caches
    // ClosedRegionDetector
    private static AccessTools.FieldRef<object> closedRegionDetectorCacheField;

    // EncloseThingsCache
    private static AccessTools.FieldRef<object, IDictionary> encloseThingsCacheField;

    // PawnPositionCache
    private static FieldInfo positionsCacheField;
    private static FieldInfo lastPositionsCacheCleanupField;

    // 1.6+ per-map MapComponent caches
    private static MethodInfo encloseThingsGetCacheMethod;
    private static MethodInfo pawnPositionGetCacheMethod;
    private static FieldInfo encloseThingsMapCacheField;
    private static FieldInfo pawnPositionMapCacheField;
    private static FieldInfo pawnPositionLastCleanupField;

    private static Action clearSecondaryCaches;

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
            workGiverScannerCacheField = AccessTools.StaticFieldRefAccess<IDictionary>(
                AccessTools.DeclaredField(type, "cache"));

            // SC 1.6 moved enclose/pawn caches from static fields to per-map MapComponents.
            if (UsesMapComponentLayout())
            {
                InitMapComponentCaches();
                clearSecondaryCaches = ClearMapComponentCaches;
            }
            else
            {
                InitStaticCaches();
                clearSecondaryCaches = ClearStaticCaches;
            }

            constructFinishFrameCurrentTickField = AccessTools.StaticFieldRefAccess<int>(
                AccessTools.DeclaredField(
                    "SmarterConstruction.Patches.WorkGiver_ConstructFinishFrames_JobOnThing:currentTick"));

            MpCompat.harmony.Patch(
                AccessTools.DeclaredMethod(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(SmarterConstruction), nameof(ClearCache)));
        }
    }

    private static bool UsesMapComponentLayout()
    {
        var encloseThingsCacheType = AccessTools.TypeByName("SmarterConstruction.Core.EncloseThingsCache");
        return encloseThingsCacheType != null
               && typeof(MapComponent).IsAssignableFrom(encloseThingsCacheType)
               && AccessTools.Method(
                   encloseThingsCacheType,
                   "GetCache",
                   new[]
                   {
                       typeof(Map)
                   }) != null;
    }

    private static void InitStaticCaches()
    {
        closedRegionDetectorCacheField = AccessTools.StaticFieldRefAccess<object>(
            AccessTools.DeclaredField("SmarterConstruction.Core.ClosedRegionDetector:cache"));
        encloseThingsCacheField =
            AccessTools.FieldRefAccess<IDictionary>("SmarterConstruction.Core.EncloseThingsCache:cache");

        var pawnPositionCacheType = AccessTools.TypeByName("SmarterConstruction.Core.PawnPositionCache");
        // Harmony seems to fail to create FieldRef<T> on those 2, so just gonna use FieldInfo instead.
        positionsCacheField = AccessTools.DeclaredField(pawnPositionCacheType, "positionCache");
        lastPositionsCacheCleanupField = AccessTools.DeclaredField(pawnPositionCacheType, "lastCacheCleanup");
    }

    private static void InitMapComponentCaches()
    {
        var encloseThingsCacheType = AccessTools.TypeByName("SmarterConstruction.Core.EncloseThingsCache");
        var pawnPositionCacheType = AccessTools.TypeByName("SmarterConstruction.Core.PawnPositionCache");

        encloseThingsGetCacheMethod = AccessTools.Method(
            encloseThingsCacheType,
            "GetCache",
            new[]
            {
                typeof(Map)
            });
        pawnPositionGetCacheMethod = AccessTools.Method(
            pawnPositionCacheType,
            "GetCache",
            new[]
            {
                typeof(Map)
            });

        encloseThingsMapCacheField = AccessTools.DeclaredField(encloseThingsCacheType, "_cache")
                                     ?? AccessTools.DeclaredField(encloseThingsCacheType, "cache");
        pawnPositionMapCacheField = AccessTools.DeclaredField(pawnPositionCacheType, "_positionCache")
                                    ?? AccessTools.DeclaredField(pawnPositionCacheType, "positionCache");
        pawnPositionLastCleanupField = AccessTools.DeclaredField(pawnPositionCacheType, "_lastCacheCleanup")
                                       ?? AccessTools.DeclaredField(pawnPositionCacheType, "lastCacheCleanup");
    }

    private static void ClearCache()
    {
        workGiverScannerCacheField().Clear();
        clearSecondaryCaches();

        // Small chance it could cause issues when the loaded game is on the same tick as the tick stored by this field
        constructFinishFrameCurrentTickField() = -1;
        // Since we set the current tick to -1 (as opposed to 0), there's no chance unless something
        // is very wrong for `thingsUpdatedThisTick` field to be valid and kept by the mod. No need to clean it.
    }

    private static void ClearStaticCaches()
    {
        encloseThingsCacheField(closedRegionDetectorCacheField()).Clear();

        ((IDictionary)positionsCacheField.GetValue(null)).Clear();
        // Reset the last cleanup tick field, as if we load an older save - it'll have to wait until the previously assigned tick to clean up.
        lastPositionsCacheCleanupField.SetValue(null, 0);
    }

    private static void ClearMapComponentCaches()
    {
        foreach (var map in Find.Maps)
        {
            var encloseThingsCache = encloseThingsGetCacheMethod.Invoke(
                null,
                new object[]
                {
                    map
                });
            if (encloseThingsCache != null)
            {
                ((IDictionary)encloseThingsMapCacheField.GetValue(encloseThingsCache)).Clear();
            }

            var pawnPositionCache = pawnPositionGetCacheMethod.Invoke(
                null,
                new object[]
                {
                    map
                });
            if (pawnPositionCache != null)
            {
                ((IDictionary)pawnPositionMapCacheField.GetValue(pawnPositionCache)).Clear();
                pawnPositionLastCleanupField.SetValue(pawnPositionCache, 0);
            }
        }
    }
}