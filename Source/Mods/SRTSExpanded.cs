using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// <para>SRTS Expanded by Smash Phil, Aquamarine, Neceros, more</para>
    /// <para>Carryalls | Intercontinental Transport by Nephlite</para>
    /// <para>Transport Shuttle Standalone by Azazellz</para>
    /// </summary>
    /// <see href="https://github.com/Neceros/SRTS-Expanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845423808"/>
    /// <see href="https://github.com/RealTelefonmast/RWCarryall"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2901034783"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2834132683"/>
    [MpCompatFor("smashphil.srtsexpanded")]
    [MpCompatFor("smashphil.neceros.srtsexpanded")]
    [MpCompatFor("Shashlichnik.srtsexpanded")]
    [MpCompatFor("Nephlite.Carryalls")]
    [MpCompatFor("Azazellz.TransportShuttleStandalone")]
    class SRTSExpanded
    {
        private static FastInvokeHandler tryLaunchMethod;
        private static AccessTools.FieldRef<object, Caravan> caravanField;
        private static ISyncField bombTypeSync;

        public SRTSExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(() => DelayedPatch(mod.PackageId));

            // Refuel shuttle
            MpCompat.RegisterLambdaDelegate("SRTS.StartUp", "LaunchAndBombGizmosPassthrough", 3);

            // Bombing
            MP.RegisterSyncWorker<Pair<IntVec3, IntVec3>>(SyncIntVec3Pair, typeof(Pair<IntVec3, IntVec3>));
            var type = AccessTools.TypeByName("SRTS.CompBombFlyer");
            MP.RegisterSyncMethod(type, "TryLaunchBombRun");
            bombTypeSync = MP.RegisterSyncField(type, "bombType");

            foreach (MethodInfo method in MpMethodUtil.GetLambda(type, nameof(ThingComp.CompGetGizmosExtra), MethodType.Normal, null, 1, 2))
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreSyncBombType)),
                    postfix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PostSyncBombType)));
        }

        private static void DelayedPatch(string modId)
        {
            // Launching the shuttle
            var type = AccessTools.TypeByName("SRTS.CompLaunchableSRTS");
            caravanField = AccessTools.FieldRefAccess<Caravan>(type, "carr");
            var tryLaunch = AccessTools.Method(type, "TryLaunch");
            tryLaunchMethod = MethodInvoker.GetHandler(tryLaunch);

            PatchingUtilities.InitCancelInInterface();
            MpCompat.harmony.Patch(tryLaunch, prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreTryLaunch)));
            MP.RegisterSyncMethod(typeof(SRTSExpanded), nameof(SyncedLaunch)).ExposeParameter(2);

            // Patch stuff unique to carryalls
            if (modId == "Nephlite.Carryalls".ToLower())
                PatchCarryalls();
        }

        private static void PreSyncBombType(ThingComp __instance)
        {
            if (MP.IsInMultiplayer)
            {
                MP.WatchBegin();
                bombTypeSync.Watch(__instance);
            }
        }

        private static void PostSyncBombType()
        {
            if (MP.IsInMultiplayer)
                MP.WatchEnd();
        }

        private static bool PreTryLaunch(ThingComp __instance, int destinationTile, TransportPodsArrivalAction arrivalAction, Caravan cafr = null)
        {
            // Let the method run only if it's synced call
            if (!MP.IsInMultiplayer || PatchingUtilities.ShouldCancel)
                return true;

            var caravanFieldValue = caravanField(__instance);
            SyncedLaunch(__instance, destinationTile, arrivalAction, cafr, caravanFieldValue);

            return false;
        }

        private static void SyncedLaunch(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction arrivalAction, Caravan caravanMethodParameter, Caravan caravanFieldValue)
        {
            caravanField(compLaunchableSrts) = caravanFieldValue;
            tryLaunchMethod(compLaunchableSrts, destinationTile, arrivalAction, caravanMethodParameter);
        }

        private static void SyncIntVec3Pair(SyncWorker sync, ref Pair<IntVec3, IntVec3> pair)
        {
            if (sync.isWriting)
            {
                sync.Write(pair.First);
                sync.Write(pair.Second);
            }
            else
                pair = new Pair<IntVec3, IntVec3>(sync.Read<IntVec3>(), sync.Read<IntVec3>());
        }

        #region Carryalls

        // CompLaunchableSRTS
        private static FastInvokeHandler boardShuttleMethod;
        private static AccessTools.FieldRef<ThingComp, List<Pawn>> tmpAllowedPawnsField;
        // Designator_AddToCarryall
        private static Type designatorAddToCarryallType;
        private static AccessTools.FieldRef<Designator, ThingComp> designatorLaunchableField;

        private static void PatchCarryalls()
        {
            var type = AccessTools.TypeByName("SRTS.CompLaunchableSRTS");
            tmpAllowedPawnsField = AccessTools.FieldRefAccess<List<Pawn>>(type, "tmpAllowedPawns");

            // Rotate, self-destruct (Kirov only)
            MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0, 1);
            // Add pawn
            MpCompat.RegisterLambdaDelegate(type, nameof(ThingComp.CompFloatMenuOptions), 0);
            // Add selected pawns
            var boardShuttle = MpMethodUtil.GetLambda(type, nameof(ThingComp.CompMultiSelectFloatMenuOptions), lambdaOrdinal: 0);
            boardShuttleMethod = MethodInvoker.GetHandler(boardShuttle);
            MpCompat.harmony.Patch(boardShuttle, prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreAddPawns)));
            MP.RegisterSyncMethod(typeof(SRTSExpanded), nameof(SyncedAddPawns));

            // Designator
            type = designatorAddToCarryallType = AccessTools.TypeByName("SRTS.Designator_AddToCarryall");
            designatorLaunchableField = AccessTools.FieldRefAccess<ThingComp>(type, "launchable");
            MP.RegisterSyncWorker<Designator>(SyncAddToCarryallDesignator, type);
        }

        private static bool PreAddPawns(ThingComp __instance, List<Pawn> ___tmpAllowedPawns)
        {
            // Let the method run only if it's synced call
            if (!MP.IsInMultiplayer || PatchingUtilities.ShouldCancel)
                return true;

            SyncedAddPawns(__instance, ___tmpAllowedPawns);
            return false;
        }

        private static void SyncedAddPawns(ThingComp comp, List<Pawn> pawns)
        {
            ref var currentFieldValue = ref tmpAllowedPawnsField(comp);
            // Set the list in the comp to the one we got, and store the previous value in the temp value (swap)
            (currentFieldValue, pawns) = (pawns, currentFieldValue);
            boardShuttleMethod(comp);
            // Restore the original list of pawns
            currentFieldValue = pawns;
        }

        private static void SyncAddToCarryallDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
                sync.Write(designatorLaunchableField(designator));
            else
                designator = (Designator)Activator.CreateInstance(designatorAddToCarryallType, sync.Read<ThingComp>());
        }

        #endregion
    }
}