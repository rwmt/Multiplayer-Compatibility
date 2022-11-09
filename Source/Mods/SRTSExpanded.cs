using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>SRTS Expanded by Smash Phil, Aquamarine, Neceros, more</summary>
    /// <see href="https://github.com/Neceros/SRTS-Expanded"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1845423808"/>
    [MpCompatFor("smashphil.srtsexpanded")]
    [MpCompatFor("smashphil.neceros.srtsexpanded")]
    class SRTSExpanded
    {
        private static MethodInfo tryLaunchMethod;
        private static AccessTools.FieldRef<object, Caravan> caravanField;
        private static ISyncField bombTypeSync;

        public SRTSExpanded(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(DelayedPatch);

            // Refuel shuttle
            MP.RegisterSyncDelegate(AccessTools.TypeByName("SRTS.StartUp"), "<>c__DisplayClass24_1", "<LaunchAndBombGizmosPassthrough>b__3");

            // Bombing
            MP.RegisterSyncWorker<Pair<IntVec3, IntVec3>>(SyncIntVec3Pair, typeof(Pair<IntVec3, IntVec3>));
            var type = AccessTools.TypeByName("SRTS.CompBombFlyer");
            MP.RegisterSyncMethod(type, "TryLaunchBombRun");
            bombTypeSync = MP.RegisterSyncField(type, "bombType");

            foreach (MethodInfo method in MpMethodUtil.GetLambda(type, "CompGetGizmosExtra", MethodType.Normal, null, 1, 2))
                MpCompat.harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreSyncBombType)),
                    postfix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PostSyncBombType)));
        }

        private static void DelayedPatch()
        {
            // Launching the shuttle
            var type = AccessTools.TypeByName("SRTS.CompLaunchableSRTS");
            caravanField = AccessTools.FieldRefAccess<Caravan>(type, "carr");
            tryLaunchMethod = AccessTools.Method(type, "TryLaunch");

            MpCompat.harmony.Patch(tryLaunchMethod, prefix: new HarmonyMethod(typeof(SRTSExpanded), nameof(PreTryLaunch)));
            MP.RegisterSyncMethod(typeof(SRTSExpanded), nameof(SyncedLaunch)).ExposeParameter(2);
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
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            var caravanFieldValue = caravanField(__instance);
            SyncedLaunch(__instance, destinationTile, arrivalAction, cafr, caravanFieldValue);

            return false;
        }

        private static void SyncedLaunch(ThingComp compLaunchableSrts, int destinationTile, TransportPodsArrivalAction arrivalAction, Caravan caravanMethodParameter, Caravan caravanFieldValue)
        {
            caravanField(compLaunchableSrts) = caravanFieldValue;
            tryLaunchMethod.Invoke(compLaunchableSrts, new object[] { destinationTile, arrivalAction, caravanMethodParameter });
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
    }
}