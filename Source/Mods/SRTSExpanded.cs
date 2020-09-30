using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>SRTS Expanded by Smash Phil, Aquamarine, Neceros, more</summary>
    /// <see href="https://github.com/Neceros/SRTS-Expanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845423808"/>
    [MpCompatFor("smashphil.neceros.srtsexpanded")]
    class SRTSExpanded
    {
        private static bool isSyncing = false;
        private static MethodInfo tryLaunchMethod;
        private static ISyncField bombTypeSync;

        public SRTSExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(DelayedPatch);

            // Refuel shuttle
            MP.RegisterSyncDelegate(AccessTools.TypeByName("SRTS.StartUp"), "<>c__DisplayClass24_1", "<LaunchAndBombGizmosPassthrough>b__3");

            // Bombing
            var type = AccessTools.TypeByName("SRTS.CompBombFlyer");
            MP.RegisterSyncMethod(type, "TryLaunchBombRun");
            bombTypeSync = MP.RegisterSyncField(type, "bombType");

            foreach (MethodInfo method in MpCompat.MethodsByIndex(type, "<CompGetGizmosExtra>", 1, 2))
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreSyncBombType)),
                    postfix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PostSyncBombType)));
        }

        private static void DelayedPatch()
        {
            // Launching the shuttle
            tryLaunchMethod = AccessTools.Method(AccessTools.TypeByName("SRTS.CompLaunchableSRTS"), "TryLaunch");
            MpCompat.harmony.Patch(tryLaunchMethod,
                prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreTryLaunch)));

            foreach (var method in new[] { nameof(SyncLandInSpecificCell), nameof(SyncFormCaravan), nameof(SyncAttackSettlement), nameof(SyncGiveGift), nameof(SyncVisitSettlement), nameof(SyncVisitSite), nameof(SyncGiveToCaravan), nameof(SyncShuttle) })
                MP.RegisterSyncMethod(typeof(SRTSExpanded), method);
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
            if (!MP.IsInMultiplayer || isSyncing)
            {
                isSyncing = false;
                return true;
            }

            isSyncing = true;

            if (arrivalAction is TransportPodsArrivalAction_LandInSpecificCell landInSpecificCell)
                SyncLandInSpecificCell(__instance, destinationTile, landInSpecificCell, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_VisitSettlement visitSettlement)
                SyncVisitSettlement(__instance, destinationTile, visitSettlement, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_FormCaravan formCaravan)
                SyncFormCaravan(__instance, destinationTile, formCaravan, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_AttackSettlement attackSettlement)
                SyncAttackSettlement(__instance, destinationTile, attackSettlement, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_GiveGift giveGift)
                SyncGiveGift(__instance, destinationTile, giveGift, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_VisitSite visitSite)
                SyncVisitSite(__instance, destinationTile, visitSite, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_GiveToCaravan giveToCaravan)
                SyncGiveToCaravan(__instance, destinationTile, giveToCaravan, cafr);
            else if (arrivalAction is TransportPodsArrivalAction_Shuttle shuttle)
                SyncShuttle(__instance, destinationTile, shuttle, cafr);
            else
                Log.Error($"Unsupported multiplayer SRTS arrival action of type {arrivalAction.GetType()}");

            return false;
        }

        private static void SyncLandInSpecificCell(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_LandInSpecificCell arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncFormCaravan(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_FormCaravan arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncAttackSettlement(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_AttackSettlement arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncGiveGift(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_GiveGift arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncVisitSettlement(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_VisitSettlement arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncVisitSite(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_VisitSite arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncGiveToCaravan(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_GiveToCaravan arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });

        private static void SyncShuttle(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction_Shuttle arrivalAction, Caravan cafr)
            => tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, cafr });
    }
}
